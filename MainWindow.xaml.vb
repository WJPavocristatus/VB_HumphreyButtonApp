Imports Phidget22
Imports Phidget22.Events
Imports System.Threading
Imports System.Threading.Tasks
Imports System.Windows.Threading
Imports System.Media

Public Class MainWindow

    ' -----------------------------
    ' Phidget Channels
    ' -----------------------------
    Private bc As New DigitalInput()   ' Button
    Private cc As New DigitalOutput()  ' Clicker (rumble)
    Private fc As New DigitalOutput()  ' Feeder
    Private flc As New DigitalOutput() ' Feeder LED
    Private llc As New DigitalOutput() ' Lockout LED

    ' -----------------------------
    ' Timer & State Variables
    ' -----------------------------
    Friend WithEvents timer As New System.Timers.Timer(16)

    Private rumbleCts As CancellationTokenSource
    Private TargetTime As Integer
    Private btnCount As Integer = 0
    Private isLockout As Boolean = False
    Private newTrialReady As Boolean = False   ' <--- REQUIRED BUTTON RELEASE BEFORE NEW TRIAL

    ' -----------------------------
    ' Stopwatches
    ' -----------------------------
    Private Latency As New Stopwatch()
    Private ActiveStimWatch As New Stopwatch()
    Private StimAWatch As New Stopwatch()
    Private StimBWatch As New Stopwatch()

    ' -------------------------------------------------------
    ' Constructor
    ' -------------------------------------------------------
    Public Sub New()
        InitializeComponent()

        bc.DeviceSerialNumber = 705800
        cc.DeviceSerialNumber = 705800
        fc.DeviceSerialNumber = 705800
        flc.DeviceSerialNumber = 705800
        llc.DeviceSerialNumber = 705800

        bc.Channel = 0
        cc.Channel = 6
        fc.Channel = 7
        flc.Channel = 9
        llc.Channel = 8

        AddHandler bc.Attach, AddressOf OnAttachHandler
        AddHandler cc.Attach, AddressOf OnAttachHandler
        AddHandler fc.Attach, AddressOf OnAttachHandler

        AddHandler bc.StateChange, AddressOf ButtonStim_StateChanged
        AddHandler bc.StateChange, AddressOf ButtonRumble_StateChanged

        cc.Open()
        bc.Open()
        fc.Open()
        flc.Open()
        llc.Open()

        isLockout = True         ' Start disabled until Start button
        newTrialReady = False    ' Must release button first

        timer.Start()
    End Sub


    ' -------------------------------------------------------
    ' Window Init
    ' -------------------------------------------------------
    Private Sub InitMainWindow() Handles MyBase.Initialized
        MainWin.Width = SystemParameters.PrimaryScreenWidth * 2
        MainWin.Height = SystemParameters.PrimaryScreenHeight
        MainWin.WindowStyle = WindowStyle.None
        MainWin.ResizeMode = ResizeMode.NoResize
    End Sub


    ' -------------------------------------------------------
    ' Start Button
    ' -------------------------------------------------------
    Private Sub BtnStart_Click(sender As Object, e As RoutedEventArgs) Handles StBtn.Click
        isLockout = False
        newTrialReady = False
    End Sub


    ' -------------------------------------------------------
    ' Timer tick
    ' -------------------------------------------------------
    Private Sub Clock() Handles timer.Elapsed
        Application.Current.Dispatcher.BeginInvoke(AddressOf ControlLoop)
    End Sub


    ' -------------------------------------------------------
    ' Stim Button Logic
    ' -------------------------------------------------------
    Private Sub ButtonStim_StateChanged(sender As Object, e As DigitalInputStateChangeEventArgs)
        Dispatcher.Invoke(Sub()

                              ' If lockout OR not ready for new trial → ignore presses
                              If isLockout Or Not newTrialReady Then

                                  ' If button is released → mark new trial ready
                                  If e.State = False Then
                                      newTrialReady = True
                                  End If

                                  Return
                              End If

                              ' ---------------------
                              ' Button Pressed
                              ' ---------------------
                              If e.State = True Then
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
                                      StimGrid.Background = Brushes.Black
                                      StimSpy.Background = Brushes.Black
                                  End If

                              Else
                                  ' ---------------------
                                  ' Button Released
                                  ' ---------------------
                                  cc.State = False
                                  Latency.Start()
                                  ActiveStimWatch.Stop()
                                  StimAWatch.Stop()
                                  StimBWatch.Stop()

                                  StimGrid.Background = Brushes.Black
                                  StimSpy.Background = Brushes.Black

                                  If Not isLockout Then
                                      RecordData()
                                  End If
                              End If

                          End Sub)
    End Sub


    ' -------------------------------------------------------
    ' Rumble Logic
    ' -------------------------------------------------------
    Private Sub ButtonRumble_StateChanged(sender As Object, e As DigitalInputStateChangeEventArgs)
        Dispatcher.Invoke(Async Function()

                              ' If trial not allowed → no rumble
                              If isLockout Or Not newTrialReady Then

                                  If e.State = False Then
                                      newTrialReady = True ' release marks ready
                                  End If

                                  Return
                              End If

                              If e.State Then
                                  rumbleCts = New CancellationTokenSource()
                                  Await RumblePak(rumbleCts.Token)
                              Else
                                  rumbleCts?.Cancel()
                                  cc.State = False
                              End If

                          End Function)
    End Sub


    ' -------------------------------------------------------
    ' Hardware Attach
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
        TargetTime = CInt(TargetTimeInput.Value) * 1000

        ' BLOCK ENTIRE TRIAL until button is released
        If Not newTrialReady Then
            Return
        End If

        ' Auto-stop if active stim hits 10 seconds
        If ActiveStimWatch.ElapsedMilliseconds >= 10000 Then
            rumbleCts?.Cancel()
            cc.State = False
            ActiveStimWatch.Stop()
            StimAWatch.Stop()
            StimBWatch.Stop()
            StimGrid.Background = Brushes.Black
            StimSpy.Background = Brushes.Black
            ActiveStimWatch.Reset()
        End If

        ' Lockout Trigger
        If Not isLockout Then
            Dim totalPress As Long = StimAWatch.ElapsedMilliseconds + StimBWatch.ElapsedMilliseconds

            If totalPress >= TargetTime Then

                ' play chime
                Try
                    Dim player As New SoundPlayer("Assets/DefaultChime.wav")
                    player.Play()
                Catch
                End Try

                isLockout = True
                newTrialReady = False     ' require button release before next trial

                Await LockOut()
                ResetTrial()
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
    ' UI Watch Updater
    ' -------------------------------------------------------
    Private Sub InitWatches()
        PressWatchVal.Content = $"{(StimAWatch.ElapsedMilliseconds + StimBWatch.ElapsedMilliseconds) / 1000} secs"
        ActiveStimVal.Content = $"{ActiveStimWatch.ElapsedMilliseconds / 1000} secs"
        StimAWatchVal.Content = $"{StimAWatch.ElapsedMilliseconds / 1000} secs"
        StimBWatchVal.Content = $"{StimBWatch.ElapsedMilliseconds / 1000} secs"
        LatencyVal.Content = $"{Latency.ElapsedMilliseconds} msec"
    End Sub


    ' -------------------------------------------------------
    ' Color + Texture Stim Presentation
    ' -------------------------------------------------------
    Private Sub SetGridColor(count As Integer)
        Dim even As Boolean = (count Mod 2 = 0)
        ActiveStimWatch.Start()

        If even Then
            StimAWatch.Start()
            StimGrid.Background = New ImageBrush(New BitmapImage(New Uri("Assets/playbtn.png", UriKind.Relative)))
            StimSpy.Background = New ImageBrush(New BitmapImage(New Uri("Assets/playbtn.png", UriKind.Relative)))
        Else
            StimBWatch.Start()
            StimGrid.Background = New ImageBrush(New BitmapImage(New Uri("Assets/playbtn.png", UriKind.Relative)))
            StimSpy.Background = New ImageBrush(New BitmapImage(New Uri("Assets/playbtn.png", UriKind.Relative)))
        End If
    End Sub



    ' -------------------------------------------------------
    ' LOCKOUT
    ' -------------------------------------------------------
    Public Async Function LockOut() As Task
        StimGrid.Background = Brushes.Black
        StimSpy.Background = Brushes.Black

        bc.Close()
        Latency.Stop()

        Await PlayLockoutLEDSequence()
        Await ActivateOut(fc, 50)
        Await Task.Delay(3000)

        bc.Open()

        flc.State = False
        llc.State = False

        Latency.Reset()
    End Function


    ' -------------------------------------------------------
    ' LED Animation
    ' -------------------------------------------------------
    Private Async Function PlayLockoutLEDSequence() As Task
        For i = 1 To 5
            flc.State = True
            llc.State = True
            Await Task.Delay(150)
            flc.State = False
            llc.State = False
            Await Task.Delay(150)
        Next
    End Function


    ' -------------------------------------------------------
    ' Output Pulse
    ' -------------------------------------------------------
    Private Async Function ActivateOut(chan As DigitalOutput, ms As Integer) As Task
        chan.State = True
        Await Task.Delay(ms)
        chan.State = False
    End Function


    ' -------------------------------------------------------
    ' Rumble Loop
    ' -------------------------------------------------------
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

        StimGrid.Background = Brushes.Black
        StimSpy.Background = Brushes.Black

        If Not isLockout Then
            RecordData()
        End If

        btnCount = 0
        Latency.Reset()

        ActiveStimWatch.Reset()
        StimAWatch.Reset()
        StimBWatch.Reset()
    End Sub


    ' -------------------------------------------------------
    ' Record Data
    ' -------------------------------------------------------
    Private Sub RecordData()
        TextBox1.Text &= $"{SubjectName.Text}, " &
            $"Presses: {btnCount}, " &
            $"PressDuration: {ActiveStimWatch.ElapsedMilliseconds / 1000}, " &
            $"StimA: {StimAWatch.ElapsedMilliseconds / 1000}, " &
            $"StimB: {StimBWatch.ElapsedMilliseconds / 1000}, " &
            $"Latency: {Latency.ElapsedMilliseconds}" &
            Environment.NewLine

        TextBox1.ScrollToEnd()
    End Sub


    ' -------------------------------------------------------
    ' Save Click
    ' -------------------------------------------------------
    Private Sub Save_Click(sender As Object, e As RoutedEventArgs) Handles BtnSave.Click
        Dim save As New Microsoft.Win32.SaveFileDialog With {
            .FileName = $"{SubjectName.Text}_{Date.Now.ToFileTimeUtc}.csv",
            .DefaultExt = ".csv"
        }

        If save.ShowDialog() Then
            IO.File.WriteAllText(save.FileName, TextBox1.Text)
        End If
    End Sub


    ' -------------------------------------------------------
    ' Auto Save
    ' -------------------------------------------------------
    Private Sub AutoSaveOnExit()
        Try
            Dim folder As String = IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                "PhidgetData")

            If Not IO.Directory.Exists(folder) Then
                IO.Directory.CreateDirectory(folder)
            End If

            Dim file As String = IO.Path.Combine(
                folder,
                $"{SubjectName.Text}_{Date.Now.ToFileTimeUtc}_autosave.csv")

            IO.File.WriteAllText(file, TextBox1.Text)
        Catch
        End Try
    End Sub


    ' -------------------------------------------------------
    ' Shutdown
    ' -------------------------------------------------------
    Protected Overrides Sub OnClosed(e As EventArgs)
        AutoSaveOnExit()

        bc?.Close()
        cc?.Close()
        fc?.Close()
        flc?.Close()
        llc?.Close()

        MyBase.OnClosed(e)
    End Sub

End Class
