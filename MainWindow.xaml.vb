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
    ' Flags & State Variables
    ' -----------------------------
    Friend WithEvents timer As New System.Timers.Timer(1) ' 1 ms tick

    Private rumbleCts As CancellationTokenSource
    Private TargetTime As Integer
    Private btnCount As Integer = 0
    Private trialCount As Integer = 0
    Private isLockout As Boolean = False
    Private animationPlayed As Boolean = False
    Private isRunning As Boolean = False  ' Pre-start blocking
    Private newTrialReady As Boolean = True        ' Must release after lockout
    Private prevBtnState As Boolean = False        ' For rising/falling edge detection
    Private btnHeld As Boolean = False             ' Tracks current button state

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

        bc.DeviceSerialNumber = 705800
        cc.DeviceSerialNumber = 705800
        fc.DeviceSerialNumber = 705800
        flc.DeviceSerialNumber = 705800

        bc.Channel = 0
        cc.Channel = 6
        fc.Channel = 7
        flc.Channel = 9

        AddHandler bc.Attach, AddressOf OnAttachHandler
        AddHandler fc.Attach, AddressOf OnAttachHandler
        AddHandler cc.Attach, AddressOf OnAttachHandler
        AddHandler bc.StateChange, AddressOf ButtonStim_StateChanged
        AddHandler bc.StateChange, AddressOf ButtonRumble_StateChanged

        cc.Open()
        bc.Open()
        fc.Open()
        flc.Open()

        timer.Start()
    End Sub


    ' -------------------------------------------------------
    ' Window Init
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
    ' Timer → ControlLoop
    ' -------------------------------------------------------
    Private Sub Clock() Handles timer.Elapsed
        Application.Current.Dispatcher.BeginInvoke(AddressOf ControlLoop)
    End Sub


    ' -------------------------------------------------------
    ' ButtonStim (Stimulus Logic)
    ' Now with SAFE edge detection that NEVER drops presses
    ' -------------------------------------------------------
    Private Sub ButtonStim_StateChanged(sender As Object, e As DigitalInputStateChangeEventArgs)
        Dispatcher.Invoke(Sub()

                              Dim thisPress As Boolean = e.State
                              Dim risingEdge As Boolean = (thisPress AndAlso Not prevBtnState)
                              Dim fallingEdge As Boolean = (Not thisPress AndAlso prevBtnState)

                              ' ---------------------------------------------------
                              ' REQUIRE RELEASE BEFORE NEXT TRIAL
                              ' ---------------------------------------------------
                              If Not newTrialReady Then
                                  If fallingEdge Then newTrialReady = True
                                  GoTo EndHandler
                              End If

                              ' ---------------------------------------------------
                              ' PRE-START or LOCKOUT blocks stimuli
                              ' ---------------------------------------------------
                              If isLockout OrElse Not isRunning Then
                                  GoTo EndHandler
                              End If

                              ' ---------------------------------------------------
                              ' MAIN PRESS LOGIC
                              ' ---------------------------------------------------
                              If risingEdge Then
                                  btnHeld = True
                                  HideReadyIndicator()

                                  Latency.Stop()
                                  ActivateOut(cc, 35)

                                  If ActiveStimWatch.ElapsedMilliseconds < 10000 Then
                                      btnCount += 1
                                      ActiveStimWatch.Reset()
                                      SetGridColor(btnCount)
                                  Else
                                      ActiveStimWatch.Reset()
                                      StimAWatch.Reset()
                                      StimBWatch.Reset()
                                      cc.State = False
                                      ResetGridVisuals()
                                  End If
                              End If

                              If fallingEdge Then
                                  btnHeld = False

                                  If Not isLockout Then RecordData()

                                  cc.State = False
                                  Latency.Start()
                                  ActiveStimWatch.Stop()
                                  StimAWatch.Stop()
                                  StimBWatch.Stop()
                                  ResetGridVisuals()
                              End If

EndHandler:
                              prevBtnState = thisPress
                          End Sub)
    End Sub


    ' -------------------------------------------------------
    ' Rumble Logic — also edge-safe, same gating rules
    ' -------------------------------------------------------
    Private Sub ButtonRumble_StateChanged(sender As Object, e As DigitalInputStateChangeEventArgs)
        Dispatcher.Invoke(Async Function()

                              Dim thisPress As Boolean = e.State
                              Dim risingEdge As Boolean = (thisPress AndAlso Not prevBtnState)
                              Dim fallingEdge As Boolean = (Not thisPress AndAlso prevBtnState)

                              If Not newTrialReady Then
                                  If fallingEdge Then newTrialReady = True
                                  prevBtnState = thisPress
                                  Return
                              End If

                              If isLockout OrElse Not isRunning Then
                                  prevBtnState = thisPress
                                  Return
                              End If

                              If risingEdge Then
                                  rumbleCts = New CancellationTokenSource()
                                  Await RumblePak(rumbleCts.Token)
                              End If

                              If fallingEdge Then
                                  rumbleCts?.Cancel()
                                  cc.State = False
                              End If

                              prevBtnState = thisPress
                          End Function)
    End Sub


    ' -------------------------------------------------------
    ' Phidget Attach
    ' -------------------------------------------------------
    Private Sub OnAttachHandler(sender As Object, e As AttachEventArgs)
        Dispatcher.Invoke(Sub()
                              Console.WriteLine($"Phidget {sender} attached!")
                          End Sub)
    End Sub


    ' -------------------------------------------------------
    ' CONTROL LOOP
    ' -------------------------------------------------------
    Private Async Sub ControlLoop()

        ' Pre-start behaves like lockout but without outputs
        If Not isRunning Then
            ActiveStimWatch.Stop()
            StimAWatch.Stop()
            StimBWatch.Stop()
            InitWatches()
            Return
        End If

        MasterStopWatch.Start()

        TargetTime = CInt(TargetTimeInput.Value) * 1000

        ' AUTO STOP at 10 seconds
        If ActiveStimWatch.ElapsedMilliseconds >= 10000 Then
            rumbleCts?.Cancel()
            cc.State = False
            ActiveStimWatch.Stop()
            StimAWatch.Stop()
            StimBWatch.Stop()
            ResetGridVisuals()
            ActiveStimWatch.Reset()
        End If

        ' -----------------------------
        ' TRIAL LOGIC
        ' -----------------------------
        If Not isLockout Then

            ' No trial if button not held
            If Not bc.State Then
                ActiveStimWatch.Stop()
                StimAWatch.Stop()
                StimBWatch.Stop()
                InitWatches()
                Return
            End If

            Dim totalPress As Long = StimAWatch.ElapsedMilliseconds + StimBWatch.ElapsedMilliseconds

            ' ---------------------------------------------------
            ' REACHED TARGET TIME
            ' ---------------------------------------------------
            If totalPress >= TargetTime Then

                PlaySound(IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets\beepBeep.wav"))

                isLockout = True
                rumbleCts?.Cancel()
                cc.State = False
                RecordData()
                MasterStopWatch.Stop()
                ActiveStimWatch.Stop()
                StimAWatch.Stop()
                StimBWatch.Stop()

                newTrialReady = False

                Await LockOut()

                ResetTrial()
                isLockout = False
                animationPlayed = False

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
    ' Play WAV
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
    ' UI Watch Labels
    ' -------------------------------------------------------
    Private Sub InitWatches()
        PressWatchVal.Content = $"{(StimAWatch.ElapsedMilliseconds + StimBWatch.ElapsedMilliseconds) / 1000} secs"
        ActiveStimVal.Content = $"{ActiveStimWatch.ElapsedMilliseconds / 1000} secs"
        StimAWatchVal.Content = $"{StimAWatch.ElapsedMilliseconds / 1000} secs"
        StimBWatchVal.Content = $"{StimBWatch.ElapsedMilliseconds / 1000} secs"
        LatencyVal.Content = $"{Latency.ElapsedMilliseconds} msec"
    End Sub


    ' -------------------------------------------------------
    ' Stim Color + Overlay
    ' -------------------------------------------------------
    Private Sub SetGridColor(count As Integer)
        If isLockout OrElse Not isRunning Then Return

        Dim even As Boolean = (count Mod 2 = 0)
        ActiveStimWatch.Start()

        If even Then
            StimAWatch.Start()
            StimGrid.Background = Brushes.Gray
            StimSpy.Background = Brushes.Gray
            ShowOverlay(StimGridOverlay, "Assets/invert_hd-wallpaper-7939241_1280.png")
        Else
            StimBWatch.Start()
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
    ' Ready Overlay
    ' -------------------------------------------------------
    Private Sub ShowReadyIndicator()
        StimGridReadyOverlay.Source = New BitmapImage(New Uri("Assets/playbtn.png", UriKind.Relative))
        StimGridReadyOverlay.Visibility = Visibility.Visible
    End Sub

    Private Sub HideReadyIndicator()
        StimGridReadyOverlay.Visibility = Visibility.Collapsed
    End Sub


    ' -------------------------------------------------------
    ' Lockout
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


    ' -------------------------------------------------------
    ' Reset Trial
    ' -------------------------------------------------------
    Private Sub ResetTrial()
        ActiveStimWatch.Stop()
        StimAWatch.Stop()
        StimBWatch.Stop()
        ResetGridVisuals()

        RecordData()

        trialCount += 1
        btnCount = 0

        Latency.Reset()
        Latency.Stop()

        ActiveStimWatch.Reset()
        StimAWatch.Reset()
        StimBWatch.Reset()
    End Sub


    ' -------------------------------------------------------
    ' Record CSV Row
    ' -------------------------------------------------------
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


    ' -------------------------------------------------------
    ' Manual Save
    ' -------------------------------------------------------
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
    ' Start Button
    ' -------------------------------------------------------
    Private Sub StartButton_Click(sender As Object, e As RoutedEventArgs) Handles StBtn.Click
        If Not isRunning Then
            isRunning = True
            ShowReadyIndicator()
            StBtn.Content = "Stop"
            StBtn.Background = Brushes.Violet
        Else
            isRunning = False
            HideReadyIndicator()
            StBtn.Content = "Start"
            StBtn.Background = Brushes.Red
        End If

        Latency.Reset()
        Latency.Stop()
        ActiveStimWatch.Reset()
        StimAWatch.Reset()
        StimBWatch.Reset()
        btnCount = 0
        newTrialReady = True
    End Sub


    ' -------------------------------------------------------
    ' Autosave On Exit
    ' -------------------------------------------------------
    Private Sub AutoSaveOnExit()
        Try
            Dim folder As String = IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                "PhidgetData"
            )

            If Not IO.Directory.Exists(folder) Then
                IO.Directory.CreateDirectory(folder)
            End If

            Dim file As String = IO.Path.Combine(
                folder,
                $"{SubjectName.Text}_StimA-{StimAName.Text}_StimB-{StimBName.Text}_{Date.Now.ToFileTimeUtc}.csv"
            )

            IO.File.WriteAllText(file, TextBox1.Text)

        Catch
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
