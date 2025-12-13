Imports Phidget22
Imports Phidget22.Events
Imports System.Threading
Imports System.Windows.Threading
Imports System.Media
Imports System.IO
'Imports System.Windows.Forms

''' <summary>
''' WORKING MVP VERSION OF APP!!!!
''' </summary>
Public Class MainWindow

    ' -----------------------------
    ' Phidget Channels
    ' -----------------------------
    Private bc As New DigitalInput()   ' Button Channel
    Private cc As New DigitalOutput()  ' Clicker (rumble)
    Private fc As New DigitalOutput()  ' Feeder Channel
    Private flc As New DigitalOutput() ' Feeder LED

    ' -----------------------------
    ' Timer & State Variables
    ' -----------------------------
    Friend WithEvents timer As New System.Timers.Timer(1) ' 1 ms tick

    Private rumbleCts As CancellationTokenSource
    Private TargetTime As Integer
    Private HoldLimit As Integer = 5000
    Private btnCount As Integer = 0
    Private trialCount As Integer = 0
    Private isLockout As Boolean = False
    Private animationPlayed As Boolean = False
    Private isRunning As Boolean = False ' Pre-start flag
    Private aPressCt As Integer = 0
    Private bPressCt As Integer = 0
    Private trialReady As Boolean = False

    Private sessionStartTimeStamp As DateTime

    ' One-shot guard to prevent multiple saves on multi-channel disconnect
    Private hasSavedOnDisconnect As Boolean = False

    Private manualSave As Boolean = False
    Private manualTrialSave As Boolean = False
    ' -----------------------------
    ' Stopwatches
    ' -----------------------------
    Private Latency As New Stopwatch()
    Private ActiveStimWatch As New Stopwatch()
    Private StimAWatch As New Stopwatch()
    Private StimBWatch As New Stopwatch()
    Private MasterWatch As New Stopwatch()

    ' -------------------------------------------------------
    ' Constructor
    ' -------------------------------------------------------
    Public Sub New()
        InitializeComponent()

        ' Assign device serial
        bc.DeviceSerialNumber = 705599
        cc.DeviceSerialNumber = 705599
        fc.DeviceSerialNumber = 705599
        flc.DeviceSerialNumber = 705599

        bc.Channel = 8
        cc.Channel = 14
        fc.Channel = 15
        flc.Channel = 9

        ' Events - Attach / Detach / Error
        AddHandler bc.Attach, AddressOf OnAttachHandler
        AddHandler cc.Attach, AddressOf OnAttachHandler
        AddHandler fc.Attach, AddressOf OnAttachHandler
        AddHandler flc.Attach, AddressOf OnAttachHandler

        AddHandler bc.Detach, AddressOf OnDetachHandler
        AddHandler cc.Detach, AddressOf OnDetachHandler
        AddHandler fc.Detach, AddressOf OnDetachHandler
        AddHandler flc.Detach, AddressOf OnDetachHandler

        AddHandler bc.Error, AddressOf OnErrorHandler
        AddHandler cc.Error, AddressOf OnErrorHandler
        AddHandler fc.Error, AddressOf OnErrorHandler
        AddHandler flc.Error, AddressOf OnErrorHandler

        AddHandler bc.StateChange, AddressOf ButtonStim_StateChanged
        AddHandler bc.StateChange, AddressOf ButtonRumble_StateChanged

        ' Open hardware
        Try
            cc.Open()
            bc.Open()
            fc.Open()
            flc.Open()
        Catch ex As Exception
            Console.WriteLine($"Error opening channels: {ex.Message}")
            ' If open fails, ensure we save what we have
            HandleDisconnectSave("Error opening channels: " & ex.Message)
        End Try

        ' Timer
        timer.Start()
    End Sub

    ' -------------------------------------------------------
    ' Window Placement
    ' -------------------------------------------------------
    Private Sub MainWindow_Loaded(sender As Object, e As RoutedEventArgs) Handles Me.Loaded
        Dim screens = System.Windows.Forms.Screen.AllScreens

        If screens.Length < 2 Then
            MessageBox.Show("Two monitors are required.")
            Return
        End If

        Dim researcherScreen = screens(0)
        Dim subjectScreen = screens(1)

        ' Total window spans both monitors
        Dim totalWidth = researcherScreen.Bounds.Width + subjectScreen.Bounds.Width
        Dim maxHeight = Math.Max(researcherScreen.Bounds.Height, subjectScreen.Bounds.Height)

        Me.WindowStyle = WindowStyle.None
        Me.ResizeMode = ResizeMode.NoResize
        Me.Left = researcherScreen.Bounds.Left
        Me.Top = researcherScreen.Bounds.Top
        Me.Width = totalWidth
        Me.Height = maxHeight

        ' Resize columns to fit each monitor exactly
        ResearcherView.Width = New GridLength(researcherScreen.Bounds.Width)
        SubjectView.Width = New GridLength(subjectScreen.Bounds.Width)
    End Sub



    ' -------------------------------------------------------
    ' UI Initialization
    ' -------------------------------------------------------
    Private Sub InitMainWindow() Handles MyBase.Initialized
        MainWin.Width = SystemParameters.PrimaryScreenWidth * 2
        MainWin.Height = SystemParameters.PrimaryScreenHeight
        MainWin.Top = 0
        MainWin.Left = 0
        MainWin.WindowStyle = WindowStyle.None
        MainWin.ResizeMode = ResizeMode.NoResize
    End Sub


    ' -------------------------------------------------------
    ' Clock tick → UI dispatcher → control loop
    ' -------------------------------------------------------
    Private Sub Clock() Handles timer.Elapsed
        Application.Current.Dispatcher.BeginInvoke(AddressOf ControlLoop)

    End Sub


    ' -------------------------------------------------------
    ' Button → Stimulus Logic
    ' -------------------------------------------------------
    Private Sub ButtonStim_StateChanged(sender As Object, e As DigitalInputStateChangeEventArgs)
        Dispatcher.Invoke(Sub()

                              If Not trialReady Then Return

                              ' Pre-start or lockout: ignore presses
                              If isLockout OrElse Not isRunning Then
                                  ActiveStimWatch.Stop()
                                  StimAWatch.Stop()
                                  StimBWatch.Stop()
                                  Latency.Stop()
                                  Return
                              ElseIf e.State = True Then
                                  ' Button pressed → hide ready overlay
                                  If isRunning AndAlso Not isLockout Then
                                      HideReadyIndicator()
                                  End If

                                  ' Button pressed
                                  Latency.Stop()
                                  ActivateOut(cc, 35)

                                  If ActiveStimWatch.ElapsedMilliseconds < 5000 Then
                                      btnCount += 1
                                      ActiveStimWatch.Reset()
                                      SetGridColor(btnCount)
                                  Else
                                      ' Over 5sec -> reset visual and stim
                                      ActiveStimWatch.Reset()
                                      StimAWatch.Reset()
                                      StimBWatch.Reset()
                                      cc.State = False
                                      ResetGridVisuals()
                                  End If

                              Else
                                  ' Button released
                                  If Not isLockout Then
                                      RecordData()
                                  End If
                                  cc.State = False
                                  Latency.Start()
                                  ActiveStimWatch.Stop()
                                  StimAWatch.Stop()
                                  StimBWatch.Stop()
                                  ResetGridVisuals()

                              End If

                          End Sub)
    End Sub


    ' -------------------------------------------------------
    ' Button → Rumble Loop
    ' -------------------------------------------------------
    Private Sub ButtonRumble_StateChanged(sender As Object, e As DigitalInputStateChangeEventArgs)
        Dispatcher.Invoke(Async Function()
                              If e.State Then
                                  ' Pre-start or lockout: ignore
                                  If isLockout OrElse Not isRunning Then Return
                                  rumbleCts = New CancellationTokenSource()
                                  Await RumblePak(rumbleCts.Token)
                              Else
                                  rumbleCts?.Cancel()
                                  cc.State = False
                              End If
                          End Function)
    End Sub


    ' -------------------------------------------------------
    ' Phidget Attached
    ' -------------------------------------------------------
    Private Sub OnAttachHandler(sender As Object, e As AttachEventArgs)
        Dispatcher.Invoke(Sub()
                              Console.WriteLine($"Phidget {sender} attached!")
                          End Sub)
    End Sub

    ' -------------------------------------------------------
    ' Phidget Detach (disconnect) handler
    ' -------------------------------------------------------
    Private Sub OnDetachHandler(sender As Object, e As DetachEventArgs)
        Dispatcher.Invoke(Sub()
                              Console.WriteLine($"Phidget detached: {sender}")

                              ' Save only once even if multiple channels detach
                              If hasSavedOnDisconnect Then
                                  Return
                              End If
                              hasSavedOnDisconnect = True

                              Try
                                  ' perform non-interactive autosave of both logs
                                  SaveDataAuto()
                                  SaveTrialDataAuto()
                              Catch ex As Exception
                                  Console.WriteLine($"Error saving on detach: {ex.Message}")
                              End Try

                          End Sub)
    End Sub

    ' -------------------------------------------------------
    ' Phidget Error handler (device-level errors)
    ' -------------------------------------------------------
    Private Sub OnErrorHandler(sender As Object, e As Events.ErrorEventArgs)
        Dispatcher.Invoke(Sub()
                              Console.WriteLine($"Phidget error on {sender}: {e.Description}")

                              ' Save only once on first error that we treat as critical
                              If hasSavedOnDisconnect Then
                                  Return
                              End If
                              hasSavedOnDisconnect = True

                              Try
                                  SaveDataAuto()
                                  SaveTrialDataAuto()
                              Catch ex As Exception
                                  Console.WriteLine($"Error saving on phidget error: {ex.Message}")
                              End Try

                          End Sub)
    End Sub


    ' -------------------------------------------------------
    ' CONTROL LOOP
    ' -------------------------------------------------------
    Private Async Sub ControlLoop()


        If Not trialReady Then
            HideReadyIndicator()
        End If

        ' Pre-start: behave like lockout but no outputs
        If Not isRunning Then
            ActiveStimWatch.Stop()
            StimAWatch.Stop()
            StimBWatch.Stop()
            Latency.Stop()
            InitWatches()
            Return
        End If

        TargetTime = CInt(TargetTimeInput.Value) * 1000

        ' Auto stop at HoldLimit sec
        If ActiveStimWatch.ElapsedMilliseconds >= HoldLimit Then
            rumbleCts?.Cancel()
            cc.State = False
            ActiveStimWatch.Stop()
            StimAWatch.Stop()
            StimBWatch.Stop()
            ResetGridVisuals()
            ActiveStimWatch.Reset()
        End If

        If Not isLockout Then
            If Not bc.State Then
                ActiveStimWatch.Stop()
                StimAWatch.Stop()
                StimBWatch.Stop()
                InitWatches()
                Return
            End If

            Dim totalPress As Long = StimAWatch.ElapsedMilliseconds + StimBWatch.ElapsedMilliseconds

            If totalPress >= TargetTime Then
                ' Play chime
                PlaySound(IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets\beep.wav"))

                ' Enter lockout
                isLockout = True
                rumbleCts?.Cancel()
                cc.State = False
                ActiveStimWatch.Stop()
                StimAWatch.Stop()
                StimBWatch.Stop()
                animationPlayed = True
                Await LockOut()

                isLockout = False
                animationPlayed = False
                ' Show ready overlay again after LockOut
                ShowReadyIndicator()
                Return
            End If
        End If

        If btnCount < 1 Then
            Latency.Reset()
            Latency.Stop()
        End If

        InitWatches()
    End Sub


    ' -------------------------------------------------------
    ' Play WAV file
    ' -------------------------------------------------------
    Private Sub PlaySound(fileName As String)
        Try
            Dim player As New SoundPlayer(fileName)
            player.Play()
        Catch ex As Exception
            Console.WriteLine($"Error playing sound: {ex.Message}")
        End Try
    End Sub


    ' -------------------------------------------------------
    ' Update UI watch labels
    ' -------------------------------------------------------
    Private Sub InitWatches()
        PressWatchVal.Content = $"{(StimAWatch.ElapsedMilliseconds + StimBWatch.ElapsedMilliseconds) / 1000} secs"
        ActiveStimVal.Content = $"{ActiveStimWatch.ElapsedMilliseconds / 1000} secs"
        StimAWatchVal.Content = $"{StimAWatch.ElapsedMilliseconds / 1000} secs"
        StimBWatchVal.Content = $"{StimBWatch.ElapsedMilliseconds / 1000} secs"
        LatencyVal.Content = $"{Latency.ElapsedMilliseconds / 1000} secs"
    End Sub


    ' -------------------------------------------------------
    ' Alternating colors + overlays
    ' -------------------------------------------------------
    Private Sub SetGridColor(count As Integer)
        If isLockout OrElse Not isRunning Then Return


        ActiveStimWatch.Start()
        If count Mod 2 = 0 Then
            StimAWatch.Start()
            aPressCt += 1
            StimGrid.Background = Brushes.Gray
            StimSpy.Background = Brushes.Gray
            ShowOverlay(StimGridOverlay, "Assets/invert_hd-wallpaper-7939241_1280.png")
        Else
            StimBWatch.Start()
            bPressCt += 1
            StimGrid.Background = Brushes.LightGray
            StimSpy.Background = Brushes.LightGray
            ShowOverlay(StimGridOverlay, "Assets/waves-9954690_1280.png")
        End If
    End Sub

    Private Sub ShowOverlay(img As Image, file As String)
        img.Source = New BitmapImage(New Uri(file, UriKind.Relative))
        img.Visibility = Visibility.Visible
    End Sub

    Private Sub ResetGridVisuals()
        StimGrid.Background = Brushes.Black
        StimSpy.Background = Brushes.Black
        StimGridOverlay.Visibility = Visibility.Collapsed
    End Sub

    ' -------------------------------------------------------
    ' READY overlay helpers
    ' -------------------------------------------------------
    Private Sub ShowReadyIndicator()
        If Not trialReady Then Return
        StimGridReadyOverlay.Source = New BitmapImage(New Uri("Assets/playbtn.png", UriKind.Relative))
        StimGridReadyOverlay.Visibility = Visibility.Visible
        MasterWatch.Start()
    End Sub

    Private Sub HideReadyIndicator()
        StimGridReadyOverlay.Visibility = Visibility.Collapsed
    End Sub


    ' -------------------------------------------------------
    ' Lockout sequence
    ' -------------------------------------------------------
    Public Async Function LockOut() As Task
        trialReady = False
        ResetGridVisuals()
        RecordData()
        RecordTrial()
        Try
            bc.Close()
        Catch
        End Try

        Latency.Stop()

        If Not animationPlayed Then
            animationPlayed = True
        End If
        Await PlayLockoutLEDSequence()
        Await ActivateOut(fc, 50)
        Await Task.Delay(3000)

        Try
            bc.Open()
        Catch
        End Try

        flc.State = False
        'llc.State = False
        Latency.Reset()
        Latency.Stop()
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
                cc.State = True
                Await Task.Delay(100, ct)
                cc.State = False
                Await Task.Delay(999, ct)
            End While
        Catch ex As TaskCanceledException
        End Try
    End Function

    ' -------------------------------------------------------
    ' Reset Trial
    ' -------------------------------------------------------
    Private Sub ResetTrial()
        MasterWatch.Stop()
        ActiveStimWatch.Stop()
        StimAWatch.Stop()
        StimBWatch.Stop()
        ResetGridVisuals()


        If Not isLockout Then
            RecordData()
        End If

        trialCount += 1
        btnCount = 0
        aPressCt = 0
        bPressCt = 0

        MasterWatch.Reset()
        ActiveStimWatch.Reset()
        StimAWatch.Reset()
        StimBWatch.Reset()
    End Sub

    Private Sub RecordData()
        TextBox1.Text &= $"Start Time: {sessionStartTimeStamp.ToLocalTime()}, " &
            $"{SubjectName.Text}, " &
            $"Trial Timer: {MasterWatch.ElapsedMilliseconds / 1000} secs, " &
            $"Trial: {trialCount}, " &
            $"Button Presses: {btnCount}, " &
            $"Press duration: {ActiveStimWatch.ElapsedMilliseconds / 1000} secs, " &
            $"Total StimA: {StimAWatch.ElapsedMilliseconds / 1000} secs, " &
            $"Total StimB: {StimBWatch.ElapsedMilliseconds / 1000} secs, " &
            $"Total Button Down time: {(StimAWatch.ElapsedMilliseconds + StimBWatch.ElapsedMilliseconds) / 1000} secs, " &
            $"Total Button Up time (Latency): {Latency.ElapsedMilliseconds / 1000} secs, " &
        Environment.NewLine
        TextBox1.ScrollToEnd()
    End Sub

    Private Sub RecordTrial()
        TrialDataBox.Text &= $"Start Time: {sessionStartTimeStamp.ToLocalTime()}, " &
            $"{SubjectName.Text}, " &
            $"Trial: {trialCount}, " &
            $"Button Presses: {btnCount}, " &
            $"Trial Duration: {MasterWatch.ElapsedMilliseconds / 1000} secs, " &
            $"Target Hold Time: {TargetTime}, " &
            $"Stim A Presses: {aPressCt},  " &
            $"Total StimA: {StimAWatch.ElapsedMilliseconds / 1000} secs, " &
            $"Stim B Presses: {bPressCt}, " &
            $"Total StimB: {StimBWatch.ElapsedMilliseconds / 1000} secs, " &
            $"Time to first press (Master - [Up + Down]): {(MasterWatch.ElapsedMilliseconds - (StimAWatch.ElapsedMilliseconds + StimBWatch.ElapsedMilliseconds)) / 1000} secs" &
        Environment.NewLine
        TrialDataBox.ScrollToEnd()
    End Sub



    ' -------------------------------------------------------
    ' Start button
    ' -------------------------------------------------------
    Private Sub StartButton_Click(sender As Object, e As RoutedEventArgs) Handles StBtn.Click
        sessionStartTimeStamp = DateTime.Now()
        trialReady = True
        RecordData()
        RecordTrial()
        If Not isRunning Then
            isRunning = True
            StBtn.Content = "Stop"
            StBtn.Background = Brushes.Violet
            ShowReadyIndicator()
        Else
            isRunning = False
            StBtn.Content = "Start"
            StBtn.Background = Brushes.Red
            HideReadyIndicator()
        End If

        Latency.Reset()
        ActiveStimWatch.Reset()
        StimAWatch.Reset()
        StimBWatch.Reset()
        btnCount = 0
    End Sub

    Private Sub XButton_Click(sender As Object, e As RoutedEventArgs) Handles ExitButton.Click
        If manualSave AndAlso manualTrialSave Then
            System.Windows.Application.Current.Dispatcher.InvokeShutdown()
        Else
            SaveDataAuto()
            SaveTrialDataAuto()
            System.Windows.Application.Current.Dispatcher.InvokeShutdown()
        End If
    End Sub

    Private Sub Save_Click(sender As Object, e As RoutedEventArgs) Handles BtnSave.Click
        Dim save As New Microsoft.Win32.SaveFileDialog With {
            .FileName = $"{SubjectName.Text}_StimA-{StimAName.Text}_StimB-{StimBName.Text}_{Date.Now.ToFileTimeUtc}.csv",
            .DefaultExt = ".csv"
        }
        If save.ShowDialog() Then
            IO.File.WriteAllText(save.FileName, TextBox1.Text)
        End If
        manualSave = True
    End Sub

    Private Sub Save_Trial_Click(sender As Object, e As RoutedEventArgs) Handles TrialSave.Click
        Dim save As New Microsoft.Win32.SaveFileDialog With {
            .FileName = $"{SubjectName.Text}_Trials_{Date.Now.ToFileTimeUtc}.csv",
            .DefaultExt = ".csv"
        }
        If save.ShowDialog() Then
            IO.File.WriteAllText(save.FileName, TrialDataBox.Text)
        End If
        manualTrialSave = True
    End Sub

    ' -----------------------------
    ' Non-interactive autosave helpers
    ' -----------------------------
    Private Sub SaveDataAuto()
        Try
            Dim folder As String = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "PhidgetData/autosave")
            If Not Directory.Exists(folder) Then Directory.CreateDirectory(folder)
            Dim file As String = Path.Combine(folder, $"{SubjectName.Text}_StimA-{StimAName.Text}_StimB-{StimBName.Text}_{Date.Now.ToFileTimeUtc}.csv")
            IO.File.WriteAllText(file, TextBox1.Text)
            Console.WriteLine($"Autosaved data to {file}")
        Catch ex As Exception
            Console.WriteLine($"Error autosaving data: {ex.Message}")
        End Try
    End Sub

    Private Sub SaveTrialDataAuto()
        Try
            Dim folder As String = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "PhidgetData/autosave")
            If Not Directory.Exists(folder) Then Directory.CreateDirectory(folder)
            Dim file As String = Path.Combine(folder, $"{SubjectName.Text}_Trials_{Date.Now.ToFileTimeUtc}.csv")
            IO.File.WriteAllText(file, TrialDataBox.Text)
            Console.WriteLine($"Autosaved trial data to {file}")
        Catch ex As Exception
            Console.WriteLine($"Error autosaving trial data: {ex.Message}")
        End Try
    End Sub

    Private Sub HandleDisconnectSave(reason As String)
        ' Helper to call autosave and show overlay from non-UI threads
        Dispatcher.Invoke(Sub()
                              If hasSavedOnDisconnect Or manualSave AndAlso manualTrialSave Then Return
                              hasSavedOnDisconnect = True
                              Try
                                  SaveDataAuto()
                                  SaveTrialDataAuto()
                              Catch ex As Exception
                                  Console.WriteLine($"Error saving on disconnect helper: {ex.Message}")
                              End Try
                              Console.WriteLine($"Handled disconnect save: {reason}")
                          End Sub)
    End Sub

    ' -------------------------------------------------------
    ' Clean Shutdown
    ' -------------------------------------------------------
    Protected Overrides Sub OnClosed(e As EventArgs)
        ' Ensure we try to save on normal shutdown as well
        Try
            SaveDataAuto()
            SaveTrialDataAuto()
        Catch
        End Try

        bc?.Close()
        cc?.Close()
        fc?.Close()
        flc?.Close()
        'llc?.Close()
        MyBase.OnClosed(e)
    End Sub


End Class
