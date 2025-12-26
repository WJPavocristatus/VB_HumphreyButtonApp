Imports System.IO
Imports System.Media
Imports System.Threading
Imports System.Windows.Threading
Imports System.Diagnostics
Imports System.Windows.Forms
Imports VB_HumphreyButtonApp.StimulusSequence

Imports Phidget22
Imports Phidget22.Events

Public Class MainWindow
    Private bc As New DigitalInput(), cc As New DigitalOutput(), fc As New DigitalOutput(), flc As New DigitalOutput()
    Friend WithEvents timer As New System.Timers.Timer(1)
    Private uiTimer As New DispatcherTimer With {.Interval = TimeSpan.FromMilliseconds(50)}
    Private Latency As New Stopwatch()

    Private sessionController As SessionController
    Private stimulusController As StimulusController
    Private dataController As DataController

    Private rumbleCts As CancellationTokenSource
    Private pendingDebounceCts As CancellationTokenSource
    Private lastButtonState As Boolean = False

    Private btnCount As Integer, trialCount As Integer, idx As Integer, aPressCt As Integer, bPressCt As Integer
    Private DebounceMs As Integer = 20
    Private isLockout As Boolean, isRunning As Boolean, trialReady As Boolean, TrainingMode As Boolean
    Private manualSave As Boolean, manualTrialSave As Boolean

    Private sessionStartTimeStamp As DateTime
    Private devMode As Boolean = False
    Private animationPlayed As Boolean = False

    Public Sub New()
        InitializeComponent()
        sessionController = New SessionController()
        stimulusController = New StimulusController(StimGrid, StimGridOverlay, StimGridReadyOverlay, New Stopwatch(), New Stopwatch(), New Stopwatch(), New Stopwatch(), New Stopwatch())
        dataController = New DataController()
        AddHandler sessionController.TrialCompleted, AddressOf OnSessionTrialCompleted

        bc.DeviceSerialNumber = 705599 : cc.DeviceSerialNumber = 705599 : fc.DeviceSerialNumber = 705599 : flc.DeviceSerialNumber = 705599
        bc.Channel = 8 : cc.Channel = 14 : fc.Channel = 15 : flc.Channel = 9

        AddHandler bc.Attach, AddressOf OnAttachHandler
        AddHandler bc.Detach, AddressOf OnDetachHandler
        AddHandler bc.Error, AddressOf OnErrorHandler
        AddHandler bc.StateChange, AddressOf ButtonStim_StateChanged
        AddHandler bc.StateChange, AddressOf ButtonRumble_StateChanged
        AddHandler uiTimer.Tick, AddressOf UiTimer_Tick

        Try
            cc.Open()
            bc.Open()
            fc.Open()
            flc.Open()
        Catch ex As Exception
            dataController.Log($"Error opening channels: {ex.Message}")
        End Try

        uiTimer.Start()
        timer.Start()
    End Sub

    Private Sub MainWindow_Loaded(sender As Object, e As RoutedEventArgs) Handles Me.Loaded
        Dim screens = Screen.AllScreens

        If screens.Length < 2 Then
            Dim res = System.Windows.MessageBox.Show("One monitor detected. Dev Mode?", "Confirmation", MessageBoxButton.YesNo, MessageBoxImage.Question)
            If res = MessageBoxResult.Yes Then
                devMode = True
                Dim screen = screens(0)
                Dim screenWidth = screen.Bounds.Width
                ResearcherView.Width = screenWidth
                ResearcherView.BringIntoView()
                SubjectView.Width = 0
            Else
                devMode = False
                Return
            End If
        Else
            Dim researcherScreen = screens(0)
            Dim subjectScreen = screens(1)

            Me.WindowStyle = WindowStyle.None
            Me.ResizeMode = ResizeMode.NoResize
            Me.WindowStartupLocation = WindowStartupLocation.Manual

            Me.Left = researcherScreen.Bounds.Left
            Me.Top = researcherScreen.Bounds.Top
            Me.Width = researcherScreen.Bounds.Width + subjectScreen.Bounds.Width
            Me.Height = Math.Max(researcherScreen.Bounds.Height, subjectScreen.Bounds.Height)
            StimSpy.Fill = New VisualBrush(StimGrid)

            ResearcherCol.Width = New GridLength(researcherScreen.Bounds.Width)
            SubjectCol.Width = New GridLength(subjectScreen.Bounds.Width)
        End If
    End Sub

    Private Sub UiTimer_Tick(sender As Object, e As EventArgs)
        If isRunning Then
            InitWatches()
        End If
    End Sub

    Private Sub ButtonStim_StateChanged(sender As Object, e As DigitalInputStateChangeEventArgs)
        dataController.Log($"Raw event: State={e.State}")
        Try
            pendingDebounceCts?.Cancel()
            pendingDebounceCts?.Dispose()
        Catch
        End Try
        pendingDebounceCts = New CancellationTokenSource()
        Dim captured = e.State
        Dim cts = pendingDebounceCts

        Task.Run(Async Function()
                     Try
                         Await Task.Delay(DebounceMs, cts.Token)
                         Dim actual As Boolean = False
                         Try
                             actual = bc.State
                         Catch
                             actual = captured
                         End Try

                         If actual <> captured OrElse actual = lastButtonState Then
                             Return
                         End If

                         lastButtonState = actual
                         Dispatcher.Invoke(Sub() HandleStableButton(actual))
                     Catch ex As TaskCanceledException
                         ' ignore
                     Catch ex As Exception
                         dataController.Log($"Debounce error: {ex.Message}")
                     End Try
                 End Function)
    End Sub

    Private Sub HandleStableButton(actual As Boolean)
        If Not trialReady OrElse isLockout Then Return
        If Not isRunning Then Return

        If actual Then
            stimulusController.HideReadyIndicator()
            Latency.Stop()
            ActivateOut(cc, 35)

            If sessionController.PressConfirmed() Then
                btnCount += 1
                sessionController.ActiveStimWatch.Reset()
                ApplyStimulus(btnCount)
            Else
                Try
                    rumbleCts?.Cancel()
                    rumbleCts?.Dispose()
                Catch
                End Try
                cc.State = False
                sessionController.ActiveStimWatch.Stop()
                sessionController.StimAWatch.Stop()
                sessionController.StimBWatch.Stop()
                stimulusController.EndColorWatch()
                stimulusController.ResetGridVisuals()
                sessionController.ActiveStimWatch.Reset()
            End If
        Else
            sessionController.ReleaseConfirmed()
            cc.State = False
            Latency.Start()
            sessionController.ActiveStimWatch.Stop()
            sessionController.StimAWatch.Stop()
            sessionController.StimBWatch.Stop()
            stimulusController.ResetGridVisuals()
        End If
    End Sub

    Private Sub ApplyStimulus(count As Integer)
        If TrainingMode Then
            If count Mod 2 = 0 Then
                sessionController.StopStimB()
                sessionController.StartStimA()
                aPressCt += 1
                stimulusController.SetGridColor_TRAINING(count)
            Else
                sessionController.StopStimA()
                sessionController.StartStimB()
                bPressCt += 1
                stimulusController.SetGridColor_TRAINING(count)
            End If
        Else
            Dim seq = GetSequenceForTrial(trialCount)
            If seq Is Nothing Then Return
            Dim i = idx Mod 10
            If i Mod 2 = 0 Then
                sessionController.StopStimB()
                sessionController.StartStimA()
                aPressCt += 1
            Else
                sessionController.StopStimA()
                sessionController.StartStimB()
                bPressCt += 1
            End If

            stimulusController.SetGridColor_SEQUENCE(i, seq)
            idx = (idx + 1) Mod 10
        End If
    End Sub

    Private Function GetSequenceForTrial(t As Integer) As StimulusSequence
        Select Case t
            Case 0 : Return StimulusSequence.Trial0
            Case 1 : Return StimulusSequence.Trial1
            Case 2 : Return StimulusSequence.Trial2
            Case 3 : Return StimulusSequence.Trial3
            Case 4 : Return StimulusSequence.Trial4
            Case 5 : Return StimulusSequence.Trial5
            Case 6 : Return StimulusSequence.Trial6
            Case 7 : Return StimulusSequence.Trial7
            Case 8 : Return StimulusSequence.Trial8
            Case 9 : Return StimulusSequence.Trial9
        End Select
        Return Nothing
    End Function

    Private Sub ButtonRumble_StateChanged(sender As Object, e As DigitalInputStateChangeEventArgs)
        If e.State Then
            Try
                rumbleCts?.Cancel()
                rumbleCts?.Dispose()
            Catch
            End Try
            rumbleCts = New CancellationTokenSource()
            Task.Run(Function() RumblePak(rumbleCts.Token))
        Else
            Try
                rumbleCts?.Cancel()
                rumbleCts?.Dispose()
            Catch
            End Try
            rumbleCts = Nothing
            Dispatcher.BeginInvoke(Sub() cc.State = False)
        End If
    End Sub

    Private Sub OnAttachHandler(sender As Object, e As AttachEventArgs)
        dataController.Log($"Phidget attached: {sender}")
    End Sub

    Private Sub OnDetachHandler(sender As Object, e As DetachEventArgs)
        dataController.HandleDisconnectSave(manualSave, manualTrialSave, SubjectName.Text, StimAName.Text, StimBName.Text)
    End Sub

    Private Sub OnErrorHandler(sender As Object, e As Events.ErrorEventArgs)
        dataController.HandleDisconnectSave(manualSave, manualTrialSave, SubjectName.Text, StimAName.Text, StimBName.Text)
    End Sub

    Private Async Sub ControlLoop()
        If Not trialReady Then
            stimulusController.HideReadyIndicator()
        End If

        If Not isRunning Then
            stimulusController.HideReadyIndicator()
            sessionController.ActiveStimWatch.Stop()
            stimulusController.EndColorWatch()
            sessionController.StimAWatch.Stop()
            sessionController.StimBWatch.Stop()
            Latency.Stop()
            Return
        End If

        If SubjectName.Text = "" Then SubjectName.Text = "Test"
        If TargetTimeInput.Text = "" Then TargetTimeInput.Text = "3"
        Dim target = CInt(TargetTimeInput.Text) * 1000
        sessionController.TargetTimeMs = target

        If Not isLockout Then
            If Not devMode Then
                If Not bc.Attached Then Return
                If Not bc.State Then
                    sessionController.ActiveStimWatch.Stop()
                    sessionController.StimAWatch.Stop()
                    sessionController.StimBWatch.Stop()
                    stimulusController.EndColorWatch()
                    Return
                End If
            End If
        End If

        If btnCount < 1 Then
            Latency.Reset()
            Latency.Stop()
        End If

        InitWatches()
    End Sub

    Private Sub PlaySound(fileName As String)
        Try
            Dim player As New SoundPlayer(fileName)
            player.Play()
        Catch ex As Exception
            dataController.Log($"PlaySound error: {ex.Message}")
        End Try
    End Sub

    Private Sub InitWatches()
        PressWatchVal.Content = $"{(sessionController.StimAWatch.ElapsedMilliseconds + sessionController.StimBWatch.ElapsedMilliseconds) / 1000} secs"
        ActiveStimVal.Content = $"{sessionController.ActiveStimWatch.ElapsedMilliseconds / 1000} secs"
        StimAWatchVal.Content = $"{sessionController.StimAWatch.ElapsedMilliseconds / 1000} secs"
        StimBWatchVal.Content = $"{sessionController.StimBWatch.ElapsedMilliseconds / 1000} secs"
        LatencyVal.Content = $"{Latency.ElapsedMilliseconds / 1000} secs"
    End Sub

    Public Async Function LockOut() As Task
        trialReady = False
        stimulusController.ResetGridVisuals()
        RecordData()
        RecordTrial()
        Try
            bc.Close()
        Catch
        End Try

        Latency.Stop()
        Await PlayLockoutLEDSequence()
        Await ActivateOut(fc, 50)
        Await Task.Delay(3000)

        Try
            bc.Open()
        Catch
        End Try

        flc.State = False
        Latency.Reset()
        Latency.Stop()
        sessionController.StopSession()
        ResetTrial()
        trialReady = True
    End Function

    Private Async Function PlayLockoutLEDSequence() As Task
        For i = 1 To 5
            flc.State = True
            Await Task.Delay(150)
            flc.State = False
            Await Task.Delay(150)
        Next
    End Function

    Private Async Function ActivateOut(chan As DigitalOutput, ms As Integer) As Task
        chan.State = True
        Await Task.Delay(ms)
        chan.State = False
    End Function

    Private Async Function RumblePak(ct As CancellationToken) As Task
        Try
            While Not ct.IsCancellationRequested
                Dispatcher.BeginInvoke(Sub() cc.State = True)
                Await Task.Delay(100, ct)
                Dispatcher.BeginInvoke(Sub() cc.State = False)
                Await Task.Delay(999, ct)
            End While
        Catch ex As TaskCanceledException
            Dispatcher.BeginInvoke(Sub() cc.State = False)
        End Try
    End Function

    Private Sub ResetTrial()
        sessionController.MasterWatch.Stop()
        sessionController.ActiveStimWatch.Stop()
        stimulusController.EndColorWatch()
        sessionController.StimAWatch.Stop()
        sessionController.StimBWatch.Stop()
        stimulusController.ResetGridVisuals()

        If Not isLockout Then RecordData()

        trialCount += 1
        btnCount = 0
        aPressCt = 0
        bPressCt = 0

        sessionController.ActiveStimWatch.Reset()
        sessionController.StimAWatch.Reset()
        sessionController.StimBWatch.Reset()
        sessionController.MasterWatch.Reset()
    End Sub

    Private Sub RecordData()
        Dim line = dataController.RecordData(sessionStartTimeStamp, SubjectName.Text, TrainingMode, sessionController.MasterWatch.ElapsedMilliseconds, trialCount, btnCount, sessionController.ActiveStimWatch.ElapsedMilliseconds, sessionController.StimAWatch.ElapsedMilliseconds, sessionController.StimBWatch.ElapsedMilliseconds, 0, 0, 0, 0, 0, Latency.ElapsedMilliseconds)
        TextBox1.Text &= line
        TextBox1.ScrollToEnd()
    End Sub

    Private Sub RecordTrial()
        Dim line = dataController.RecordTrial(sessionStartTimeStamp, SubjectName.Text, TrainingMode, trialCount, btnCount, sessionController.MasterWatch.ElapsedMilliseconds, CInt(sessionController.TargetTimeMs), aPressCt, sessionController.StimAWatch.ElapsedMilliseconds, bPressCt, sessionController.StimBWatch.ElapsedMilliseconds, 0, 0, 0, 0, 0)
        TrialDataBox.Text &= line
        TrialDataBox.ScrollToEnd()
    End Sub

    Private Sub SetMode() Handles TrainingToggle.Click
        TrainingMode = TrainingToggle.IsChecked
        If TrainingMode Then
            TrialSelect.Visibility = Visibility.Collapsed
        Else
            TrialSelect.Visibility = Visibility.Visible
        End If
    End Sub

    Private Sub ShowReadyIndicator()
        If Not trialReady Then Return
        stimulusController.ShowReadyIndicator()
        sessionController.MasterWatch.Start()
    End Sub

    Private Sub HideReadyIndicator()
        stimulusController.HideReadyIndicator()
    End Sub

    Private Sub StartButton_Click(sender As Object, e As RoutedEventArgs) Handles StBtn.Click
        sessionStartTimeStamp = DateTime.Now()
        trialReady = True
        RecordData()
        RecordTrial()

        If TargetTimeInput.Text = "" Then
            TargetTimeInput.Text = "3"
        End If
        If Not isRunning Then
            isRunning = True
            StBtn.Content = "Stop"
            StBtn.Background = Brushes.Red
            ShowReadyIndicator()
            sessionController.StartSession(CInt(TargetTimeInput.Text) * 1000)
        Else
            isRunning = False
            StBtn.Content = "Start"
            StBtn.Background = Brushes.Blue
            HideReadyIndicator()
            sessionController.StopSession()
        End If

        Latency.Reset()
        sessionController.ActiveStimWatch.Reset()
        sessionController.StimAWatch.Reset()
        sessionController.StimBWatch.Reset()
        btnCount = 0
    End Sub

    Private Sub XButton_Click(sender As Object, e As RoutedEventArgs) Handles ExitButton.Click
        If manualSave AndAlso manualTrialSave Then
            Application.Current.Dispatcher.InvokeShutdown()
        Else
            dataController.HandleDisconnectSave(manualSave, manualTrialSave, SubjectName.Text, StimAName.Text, StimBName.Text)
            Application.Current.Dispatcher.InvokeShutdown()
        End If
    End Sub

    Private Sub Save_Click(sender As Object, e As RoutedEventArgs) Handles BtnSave.Click
        Dim save As New Microsoft.Win32.SaveFileDialog With {.FileName = $"{SubjectName.Text}_StimA-{StimAName.Text}_StimB-{StimBName.Text}_{Date.Now.ToFileTimeUtc}.csv", .DefaultExt = ".csv"}
        If save.ShowDialog() Then
            IO.File.WriteAllText(save.FileName, TextBox1.Text)
            manualSave = True
        End If
    End Sub

    Private Sub Save_Trial_Click(sender As Object, e As RoutedEventArgs) Handles TrialSave.Click
        Dim save As New Microsoft.Win32.SaveFileDialog With {.FileName = $"{SubjectName.Text}_Trials_{Date.Now.ToFileTimeUtc}.csv", .DefaultExt = ".csv"}
        If save.ShowDialog() Then
            IO.File.WriteAllText(save.FileName, TrialDataBox.Text)
            manualTrialSave = True
        End If
    End Sub

    Protected Overrides Sub OnClosed(e As EventArgs)
        dataController.HandleDisconnectSave(manualSave, manualTrialSave, SubjectName.Text, StimAName.Text, StimBName.Text)
        Try
            bc?.Close()
            cc?.Close()
            fc?.Close()
            flc?.Close()
        Catch
        End Try
        dataController.Close()
        MyBase.OnClosed(e)
    End Sub

    Private Async Sub OnSessionTrialCompleted(sender As Object, e As EventArgs)
        Try
            ' Play chime on UI thread
            Dispatcher.Invoke(Sub() PlaySound(IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets\beep.wav")))

            ' Enter lockout and stop watches
            isLockout = True
            Try
                rumbleCts?.Cancel()
                rumbleCts?.Dispose()
            Catch
            End Try
            cc.State = False
            sessionController.ActiveStimWatch.Stop()
            stimulusController.EndColorWatch()
            sessionController.StimAWatch.Stop()
            sessionController.StimBWatch.Stop()
            animationPlayed = True

            ' Perform lockout sequence
            Await LockOut()

            isLockout = False
            animationPlayed = False
            ShowReadyIndicator()
        Catch ex As Exception
            dataController.Log($"OnSessionTrialCompleted error: {ex.Message}")
        End Try
    End Sub
End Class
