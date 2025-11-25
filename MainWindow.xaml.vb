Imports Phidget22
Imports Phidget22.Events
Imports System.Threading
Imports System.Threading.Tasks
Imports System.Windows.Threading
Imports System.Diagnostics
Imports System.Windows.Media
Imports System.Media

Public Class MainWindow

    ' -----------------------------
    ' Phidget Channels
    ' -----------------------------
    Private bc As New DigitalInput()   ' Button Channel
    Private cc As New DigitalOutput()  ' Clicker (rumble)
    Private fc As New DigitalOutput()  ' Feeder Channel
    Private flc As New DigitalOutput() ' Feeder LED
    Private llc As New DigitalOutput() ' Lockout LED

    ' -----------------------------
    ' Timer & State Variables
    ' -----------------------------
    Friend WithEvents timer As New System.Timers.Timer(16) ' 60 FPS tick

    Private rumbleCts As CancellationTokenSource
    Private TargetTime As Integer
    Private btnCount As Integer = 0
    Private isLockout As Boolean = False
    Private animationPlayed As Boolean = False

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

        ' Assign device serial
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

        ' Events
        AddHandler bc.Attach, AddressOf OnAttachHandler
        AddHandler fc.Attach, AddressOf OnAttachHandler
        AddHandler cc.Attach, AddressOf OnAttachHandler
        AddHandler bc.StateChange, AddressOf ButtonStim_StateChanged
        AddHandler bc.StateChange, AddressOf ButtonRumble_StateChanged

        ' Open hardware
        cc.Open()
        bc.Open()
        fc.Open()
        flc.Open()
        llc.Open()

        ' Timer
        timer.Start()
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

                              ' If we are in lockout, ignore presses and ensure watches are stopped/reset
                              If isLockout Then
                                  ActiveStimWatch.Stop()
                                  StimAWatch.Stop()
                                  StimBWatch.Stop()
                                  Return
                              End If

                              If e.State = True Then
                                  ' Button pressed (only handled when not in lockout)
                                  Latency.Stop()

                                  ActivateOut(cc, 35)

                                  If ActiveStimWatch.ElapsedMilliseconds < 10000 Then
                                      btnCount += 1
                                      ActiveStimWatch.Reset()
                                      SetGridColor(btnCount)
                                  Else
                                      ' Over 10sec -> reset visual and stim
                                      ActiveStimWatch.Reset()
                                      StimAWatch.Reset()
                                      StimBWatch.Reset()
                                      cc.State = False
                                      StimGrid.Background = Brushes.Black
                                      StimSpy.Background = Brushes.Black
                                  End If

                              Else
                                  ' Button released
                                  cc.State = False
                                  Latency.Start()
                                  ActiveStimWatch.Stop()
                                  StimAWatch.Stop()
                                  StimBWatch.Stop()
                                  StimGrid.Background = Brushes.Black
                                  StimSpy.Background = Brushes.Black

                                  If Not isLockout Then   ' <-- Prevent recording during lockout
                                      RecordData()
                                  End If
                              End If

                          End Sub)
    End Sub


    ' -------------------------------------------------------
    ' Button → Rumble Loop
    ' -------------------------------------------------------
    Private Sub ButtonRumble_StateChanged(sender As Object, e As DigitalInputStateChangeEventArgs)
        Dispatcher.Invoke(Async Function()
                              If e.State Then
                                  ' If in lockout, do not start rumble
                                  If isLockout Then
                                      Return
                                  End If

                                  rumbleCts = New CancellationTokenSource()
                                  ' Fire-and-await rumble until cancelled by release or lockout
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
    ' CONTROL LOOP (Safe and Clean)
    ' -------------------------------------------------------
    Private Async Sub ControlLoop()
        ' NumericUpDown gives seconds → convert to ms
        TargetTime = CInt(TargetTimeInput.Value) * 1000

        ' Auto stop if ActiveStim hits 10 sec
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

        ' If currently in lockout, don't evaluate lockout condition and don't change timing
        If Not isLockout Then
            ' Ensure we only consider timing while button is actually held
            If Not bc.State Then
                ' Button not held — ensure watches are stopped (no accumulation)
                ActiveStimWatch.Stop()
                StimAWatch.Stop()
                StimBWatch.Stop()
                InitWatches()
                Return
            End If

            Dim totalPress As Long = StimAWatch.ElapsedMilliseconds + StimBWatch.ElapsedMilliseconds

            If totalPress >= TargetTime Then
                PlaySound(IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets\DefaultChime.wav"))
                ' Enter lockout — prevent re-entry and stop timing immediately
                isLockout = True

                ' Cancel any rumble, stop clicker
                rumbleCts?.Cancel()
                cc.State = False

                ' Stop timing watches so no further accumulation
                ActiveStimWatch.Stop()
                StimAWatch.Stop()
                StimBWatch.Stop()

                ' Mark animation as played for this lockout
                animationPlayed = True

                ' Perform lockout (plays the LED animation once)
                Await LockOut()

                ' After lockout completes, reset trial and allow next trial
                ResetTrial()
                isLockout = False
                animationPlayed = False
                Return
            End If
        End If

        ' Reset latency when first press hasn't occurred
        If btnCount < 1 Then
            Latency.Reset()
            Latency.Stop()
        End If

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
    End Sub


    ' -------------------------------------------------------
    ' Alternating colors on each press
    ' -------------------------------------------------------
    Private Sub SetGridColor(count As Integer)
        ' If we're in lockout don't start any watches or change UI
        If isLockout Then
            Return
        End If

        Dim even As Boolean = (count Mod 2 = 0)

        ActiveStimWatch.Start()

        If even Then
            StimAWatch.Start()
            StimGrid.Background = Brushes.DarkGray
            StimSpy.Background = Brushes.DarkGray
        Else
            StimBWatch.Start()
            StimGrid.Background = Brushes.LightGray
            StimSpy.Background = Brushes.LightGray
        End If
    End Sub


    ' -------------------------------------------------------
    ' LED Animation + Feeder + Full Lockout
    ' -------------------------------------------------------
    Public Async Function LockOut() As Task
        StimGrid.Background = Brushes.Black
        StimSpy.Background = Brushes.Black

        ' Hard disable button input immediately
        Try
            bc.Close()
        Catch
        End Try

        Latency.Stop()

        ' PLAY LED ANIMATION ONLY HERE (only once per lockout due to animationPlayed flag)
        If Not animationPlayed Then
            ' Shouldn't happen because we set animationPlayed before calling LockOut,
            ' but keep the guard for safety.
            animationPlayed = True
        End If
        Await PlayLockoutLEDSequence()

        ' FEEDER pulse
        Await ActivateOut(fc, 50)

        ' ITI
        Await Task.Delay(3000)

        ' Restore state
        Try
            bc.Open()
        Catch
        End Try

        flc.State = False
        llc.State = False
        Latency.Reset()
    End Function


    ' -------------------------------------------------------
    ' LED Animation (only during lockout)
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
    ' Activate Output
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
    ' Reset Trial After Lockout
    ' -------------------------------------------------------
    Private Sub ResetTrial()
        ActiveStimWatch.Stop()
        StimAWatch.Stop()
        StimBWatch.Stop()

        StimGrid.Background = Brushes.Black
        StimSpy.Background = Brushes.Black

        ' Record data now that lockout has finished (we only record if not in lockout)
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
    ' Write a CSV line to TextBox
    ' -------------------------------------------------------
    Private Sub RecordData()
        TextBox1.Text &= $"{SubjectName.Text}, " &
            $"Button Presses: {btnCount}, " &
            $"Press duration: {ActiveStimWatch.ElapsedMilliseconds / 1000} secs, " &
            $"Total StimA: {StimAWatch.ElapsedMilliseconds / 1000} secs, " &
            $"Total StimB: {StimBWatch.ElapsedMilliseconds / 1000} secs, " &
            $"Latency: {Latency.ElapsedMilliseconds} msec" &
            Environment.NewLine

        TextBox1.ScrollToEnd()
    End Sub


    ' -------------------------------------------------------
    ' Save Button
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
    ' Some Helpers
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
    ' Clean Shutdown
    ' -------------------------------------------------------
    Protected Overrides Sub OnClosed(e As EventArgs)
        bc?.Close()
        cc?.Close()
        fc?.Close()
        flc?.Close()
        llc?.Close()
        MyBase.OnClosed(e)
    End Sub

End Class
