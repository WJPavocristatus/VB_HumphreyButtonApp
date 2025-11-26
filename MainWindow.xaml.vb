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

    ' -----------------------------
    ' Timer & State Variables
    ' -----------------------------
    Friend WithEvents timer As New System.Timers.Timer(10)
    Private rumbleCts As CancellationTokenSource
    Private TargetTime As Integer
    Private btnCount As Integer = 0
    Private trialCount As Integer = 0
    Private isLockout As Boolean = False
    Private animationPlayed As Boolean = False
    Private isRunning As Boolean = False
    Private trialInProgress As Boolean = False
    Private buttonPressed As Boolean = False
    Private stimulusActive As Boolean = False
    Private activeStimulus As String = ""

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

        ' Phidget serials
        bc.DeviceSerialNumber = 705599
        cc.DeviceSerialNumber = 705599
        fc.DeviceSerialNumber = 705599
        flc.DeviceSerialNumber = 705599

        bc.Channel = 1
        cc.Channel = 6
        fc.Channel = 7
        flc.Channel = 9

        AddHandler bc.Attach, AddressOf OnAttachHandler
        AddHandler fc.Attach, AddressOf OnAttachHandler
        AddHandler cc.Attach, AddressOf OnAttachHandler
        AddHandler bc.StateChange, AddressOf Button_StateChanged
        AddHandler bc.StateChange, AddressOf ButtonRumble_StateChanged

        cc.Open()
        bc.Open()
        fc.Open()
        flc.Open()

        timer.Start()

        ' Hide play button initially
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
    ' Button state tracking
    ' -------------------------------------------------------
    Private Sub Button_StateChanged(sender As Object, e As DigitalInputStateChangeEventArgs)
        Dispatcher.Invoke(Sub()
                              buttonPressed = e.State
                          End Sub)
    End Sub

    ' -------------------------------------------------------
    ' Button rumble
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
    ' Phidget attach
    ' -------------------------------------------------------
    Private Sub OnAttachHandler(sender As Object, e As AttachEventArgs)
        Dispatcher.Invoke(Sub()
                              Console.WriteLine($"Phidget {sender} attached!")
                          End Sub)
    End Sub

    ' -------------------------------------------------------
    ' Control loop
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

        ' Show ready button only if no trial in progress
        StimGridReadyOverlay.Visibility = If(Not trialInProgress, Visibility.Visible, Visibility.Collapsed)

        ' Start a new trial if button is pressed and no trial is in progress
        If buttonPressed AndAlso Not trialInProgress AndAlso Not isLockout Then
            trialInProgress = True
            btnCount += 1
            ActiveStimWatch.Restart()
            Select Case btnCount Mod 2
                Case 0 : activeStimulus = "A"
                Case 1 : activeStimulus = "B"
            End Select
            stimulusActive = True
            SetGridColor(activeStimulus)
        End If

        ' While button is held, continue stimulus
        If buttonPressed AndAlso stimulusActive Then
            ActiveStimWatch.Start()
            Select Case activeStimulus
                Case "A" : StimAWatch.Start() : StimBWatch.Stop()
                Case "B" : StimBWatch.Start() : StimAWatch.Stop()
            End Select
        End If

        ' When button is released, end stimulus
        If Not buttonPressed AndAlso stimulusActive Then
            stimulusActive = False
            ResetGridVisuals()
            ActiveStimWatch.Stop()
            StimAWatch.Stop()
            StimBWatch.Stop()
            Latency.Start()
        End If

        ' Check if total press reached target time
        TargetTime = CInt(TargetTimeInput.Value) * 1000
        Dim totalPress As Long = StimAWatch.ElapsedMilliseconds + StimBWatch.ElapsedMilliseconds
        If totalPress >= TargetTime AndAlso trialInProgress AndAlso Not isLockout Then
            ' Lockout + animation + feeder cycle
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

        ' Update UI labels
        InitWatches()
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
        MasterStopWatchVal.Content = $"{MasterStopWatch.ElapsedMilliseconds / 1000} secs"
    End Sub

    ' -------------------------------------------------------
    ' Stimuli + overlays
    ' -------------------------------------------------------
    Private Sub SetGridColor(stim As String)
        Select Case stim
            Case "A"
                StimGrid.Background = Brushes.DarkGray
                StimSpy.Background = Brushes.DarkGray
                ShowOverlay(StimGridOverlay, "Assets/invert_hd-wallpaper-7939241_1280.png")
            Case "B"
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
        activeStimulus = ""
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
        trialInProgress = False
        btnCount = 0
        ActiveStimWatch.Reset()
        StimAWatch.Reset()
        StimBWatch.Reset()
        Latency.Reset()
        Latency.Stop()
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

    ' -------------------------------------------------------
    ' Start button
    ' -------------------------------------------------------
    Private Sub StartButton_Click(sender As Object, e As RoutedEventArgs) Handles StBtn.Click
        isRunning = Not isRunning
        If isRunning Then
            StBtn.Content = "Stop"
            StBtn.Background = Brushes.Violet
        Else
            StBtn.Content = "Start"
            StBtn.Background = Brushes.Red
        End If
        ResetTrial()
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
