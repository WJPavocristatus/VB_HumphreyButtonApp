Imports Phidget22
Imports Phidget22.Events
Imports System.Threading
Imports System.Threading.Tasks
Imports System.Windows.Threading
Imports System.Diagnostics
Imports System.Windows.Media
Imports System.Windows.Media.Imaging
Imports System.Media

Public Class MainWindow

    ' -----------------------------
    ' Phidget Channels
    ' -----------------------------
    Private bc As New DigitalInput()   ' Button Channel
    Private cc As New DigitalOutput()  ' Clicker (rumble)
    Private fc As New DigitalOutput()  ' Feeder Channel
    Private flc As New DigitalOutput() ' Feeder LED
    Private btnLED As New DigitalOutput()

    ' -----------------------------
    ' Timer & State Variables
    ' -----------------------------
    Friend WithEvents timer As New System.Timers.Timer(10) ' 10 ms tick
    Private rumbleCts As CancellationTokenSource
    Private TargetTime As Integer
    Private btnCount As Integer = 0
    Private trialCount As Integer = 0
    Private isLockout As Boolean = False
    Private animationPlayed As Boolean = False
    Private isRunning As Boolean = False
    Private isTrialReady As Boolean = True

    ' -----------------------------
    ' Button press tracking
    ' -----------------------------
    Private buttonPressed As Boolean = False
    Private stimulusActive As Boolean = False ' Track if a stimulus is active for current press

    ' -----------------------------
    ' Stopwatches
    ' -----------------------------
    Private Latency As New Stopwatch()
    Private ActiveStimWatch As New Stopwatch()
    Private StimAWatch As New Stopwatch()
    Private StimBWatch As New Stopwatch()
    Private MasterStopWatch As New Stopwatch()

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

        bc.Channel = 1
        cc.Channel = 6
        fc.Channel = 7
        flc.Channel = 9

        ' Events
        AddHandler bc.Attach, AddressOf OnAttachHandler
        AddHandler fc.Attach, AddressOf OnAttachHandler
        AddHandler cc.Attach, AddressOf OnAttachHandler
        AddHandler bc.StateChange, AddressOf Button_StateChanged
        AddHandler bc.StateChange, AddressOf ButtonRumble_StateChanged

        ' Open hardware
        cc.Open()
        bc.Open()
        fc.Open()
        flc.Open()

        ' Timer
        timer.Start()

        ' Hide READY overlay initially
        StimGridReadyOverlay.Visibility = Visibility.Collapsed
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
    ' Button → State
    ' -------------------------------------------------------
    Private Sub Button_StateChanged(sender As Object, e As DigitalInputStateChangeEventArgs)
        Dispatcher.Invoke(Sub()
                              buttonPressed = e.State
                          End Sub)
    End Sub

    ' -------------------------------------------------------
    ' Button → Rumble Loop
    ' -------------------------------------------------------
    Private Sub ButtonRumble_StateChanged(sender As Object, e As DigitalInputStateChangeEventArgs)
        Dispatcher.Invoke(Async Function()
                              If e.State Then
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
    ' CONTROL LOOP – runs every timer tick
    ' -------------------------------------------------------
    Private Async Sub ControlLoop()
        If Not isRunning Then
            StimGridReadyOverlay.Visibility = Visibility.Collapsed
            ResetGridVisuals()
            ActiveStimWatch.Stop()
            StimAWatch.Stop()
            StimBWatch.Stop()
            InitWatches()
            Return
        End If

        MasterStopWatch.Start()
        TargetTime = CInt(TargetTimeInput.Value) * 1000

        ' Show READY overlay only if trial is ready and button not pressed
        If isTrialReady AndAlso Not buttonPressed Then
            StimGridReadyOverlay.Visibility = Visibility.Visible
        Else
            StimGridReadyOverlay.Visibility = Visibility.Collapsed
        End If

        ' Start new trial on button press
        If buttonPressed AndAlso isTrialReady AndAlso Not stimulusActive AndAlso Not isLockout Then
            ' Increment trial count once
            btnCount += 1
            ActiveStimWatch.Restart()
            stimulusActive = True
            isTrialReady = False

            ' Activate the selected stimulus and overlay
            SetGridColor(btnCount)
        End If

        ' Keep the stimulus displayed while button is held
        If buttonPressed AndAlso stimulusActive Then
            ActiveStimWatch.Start()
        End If

        ' Button released: end stimulus
        If Not buttonPressed AndAlso stimulusActive Then
            stimulusActive = False
            ResetGridVisuals()
            ActiveStimWatch.Stop()
            StimAWatch.Stop()
            StimBWatch.Stop()
            Latency.Start()
            ' Trial is now ready for next button press
            isTrialReady = True
        End If

        ' Check for lockout / feeder trigger
        Dim totalPress As Long = StimAWatch.ElapsedMilliseconds + StimBWatch.ElapsedMilliseconds
        If totalPress >= TargetTime AndAlso buttonPressed AndAlso Not isLockout Then
            PlaySound(IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets\beepBeep.wav"))

            isLockout = True
            stimulusActive = False
            cc.State = False
            rumbleCts?.Cancel()
            RecordData()
            MasterStopWatch.Stop()
            ActiveStimWatch.Stop()
            StimAWatch.Stop()
            StimBWatch.Stop()
            animationPlayed = True
            Await LockOut()
            ResetTrial()
            isLockout = False
            animationPlayed = False
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
        LatencyVal.Content = $"{Latency.ElapsedMilliseconds} msec"
    End Sub

    ' -------------------------------------------------------
    ' Alternating colors + overlays
    ' -------------------------------------------------------
    Private Sub SetGridColor(count As Integer)
        Select Case count Mod 2
            Case 0
                StimAWatch.Start()
                StimBWatch.Stop()
                StimGrid.Background = Brushes.DarkGray
                StimSpy.Background = Brushes.DarkGray
                ShowOverlay(StimGridOverlay, "Assets/invert_hd-wallpaper-7939241_1280.png")
            Case 1
                StimBWatch.Start()
                StimAWatch.Stop()
                StimGrid.Background = Brushes.LightGray
                StimSpy.Background = Brushes.LightGray
                ShowOverlay(StimGridOverlay, "Assets/waves-9954690_1280.png")
        End Select
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
        StimGridReadyOverlay.Source = New BitmapImage(New Uri("Assets/playbtn.png", UriKind.Relative))
        StimGridReadyOverlay.Visibility = Visibility.Visible
    End Sub

    Private Sub HideReadyIndicator()
        StimGridReadyOverlay.Visibility = Visibility.Collapsed
    End Sub

    ' -------------------------------------------------------
    ' Lockout sequence
    ' -------------------------------------------------------
    Public Async Function LockOut() As Task
        RecordData()
        ResetGridVisuals()
        Try : bc.Close() : Catch : End Try
        Latency.Stop()
        MasterStopWatch.Reset()
        If Not animationPlayed Then animationPlayed = True
        Await PlayLockoutLEDSequence()
        Await ActivateOut(fc, 50)
        Await Task.Delay(3000)
        Try : bc.Open() : Catch : End Try
        flc.State = False
        Latency.Reset()
        Latency.Stop()
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

    Private Sub ResetTrial()
        ActiveStimWatch.Stop()
        StimAWatch.Stop()
        StimBWatch.Stop()
        ResetGridVisuals()
        buttonPressed = False
        stimulusActive = False
        btnCount = 0
        Latency.Reset()
        Latency.Stop()
        ActiveStimWatch.Reset()
        StimAWatch.Reset()
        StimBWatch.Reset()
        trialCount += 1

        ' Ready overlay shown only if trial ready
        If isRunning AndAlso isTrialReady Then
            StimGridReadyOverlay.Visibility = Visibility.Visible
        End If
    End Sub

    Private Sub RecordData()
        TextBox1.Text &= $"{SubjectName.Text}, " &
            $"Trial: {trialCount}, " &
            $"Button Presses: {btnCount}, " &
            $"Total Button Down time: {(StimAWatch.ElapsedMilliseconds + StimBWatch.ElapsedMilliseconds) / 1000} secs, " &
            $"Press duration: {ActiveStimWatch.ElapsedMilliseconds / 1000} secs, " &
            $"Total StimA: {StimAWatch.ElapsedMilliseconds / 1000} secs, " &
            $"Total StimB: {StimBWatch.ElapsedMilliseconds / 1000} secs, " &
            $"Total Button Up time: {Latency.ElapsedMilliseconds / 1000} secs, " &
            $"MasterStopWatch Time: {MasterStopWatch.ElapsedMilliseconds / 1000} secs" &
        Environment.NewLine
        TextBox1.ScrollToEnd()
    End Sub

    Private Sub Save_Click(sender As Object, e As RoutedEventArgs) Handles BtnSave.Click
        Dim save As New Microsoft.Win32.SaveFileDialog With {
            .FileName = $"{SubjectName.Text}_StimA-{StimAName.Text}_StimB-{StimBName.Text}_{Date.Now.ToFileTimeUtc}.csv",
            .DefaultExt = ".csv"
        }
        If save.ShowDialog() Then
            IO.File.WriteAllText(save.FileName, TextBox1.Text)
        End If
    End Sub

    ' -------------------------------------------------------
    ' Start button
    ' -------------------------------------------------------
    Private Sub StartButton_Click(sender As Object, e As RoutedEventArgs) Handles StBtn.Click
        isRunning = Not isRunning
        isTrialReady = True
        stimulusActive = False
        buttonPressed = False

        If isRunning Then
            StimGridReadyOverlay.Visibility = Visibility.Visible
            StBtn.Content = "Stop"
            StBtn.Background = Brushes.Violet
        Else
            StimGridReadyOverlay.Visibility = Visibility.Collapsed
            StBtn.Content = "Start"
            StBtn.Background = Brushes.Red
        End If

        Latency.Reset()
        Latency.Stop()
        ActiveStimWatch.Reset()
        StimAWatch.Reset()
        StimBWatch.Reset()
        btnCount = 0
    End Sub

    ' -------------------------------------------------------
    ' Autosave
    ' -------------------------------------------------------
    Private Sub AutoSaveOnExit()
        Try
            Dim folder As String = System.IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                "PhidgetData"
            )
            If Not IO.Directory.Exists(folder) Then IO.Directory.CreateDirectory(folder)

            Dim file As String = System.IO.Path.Combine(
                folder,
                $"{SubjectName.Text}_StimA-{StimAName.Text}_StimB-{StimBName.Text}_{Date.Now.ToFileTimeUtc}.csv"
            )

            IO.File.WriteAllText(file, TextBox1.Text)
        Catch ex As Exception
        End Try
    End Sub

    ' -------------------------------------------------------
    ' Clean Shutdown
    ' -------------------------------------------------------
    Protected Overrides Sub OnClosed(e As EventArgs)
        AutoSaveOnExit()
        bc?.Close()
        cc?.Close()
        fc?.Close()
        flc?.Close()
        MyBase.OnClosed(e)
    End Sub

End Class
