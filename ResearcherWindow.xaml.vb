Imports Phidget22

Public Class ResearcherWindow
    Dim opMode As String
    Private ph As New DigitalInput()
    'Private pc As New DigitalOutput() '<-- INTENDED FOR PHYSICAL (I.E., LED) PROGRESS BA; pc = "Progress Channel"
    Private bc As New DigitalInput() 'bc = "Button Channel"
    'Private fc As New DigitalOutput() 'fc = "Feeder Channel"
    Friend WithEvents timer As New System.Timers.Timer
    Public btnCount As Integer = 0

    Public Property PressWatch As Long
    Public Property Latency As Stopwatch = New Stopwatch()
    Public Property ActiveStimWatch As Stopwatch = New Stopwatch()
    Public Property StimAWatch As Stopwatch = New Stopwatch()
    Public Property StimBWatch As Stopwatch = New Stopwatch()


    Public Sub New()

        ' This call is required by the designer.
        InitializeComponent()



        bc.DeviceSerialNumber = 705800
        'fc.DeviceSerialNumber = 705800
        bc.Channel = 1
        'fc.Channel = 15
        'pc.DeviceSerialNumber = 705800
        'pc.Channel = 2
        timer.Start()
        timer.Interval = 1
        'Log.Enable(LogLevel.Info, "file.log")

        'AddHandler bc.Attach, AddressOf OnAttachHandler
        'AddHandler VirtualBtn.Click, AddressOf Virtual_StateChange

        'AddHandler fc.Attach, AddressOf OnAttachHandler
        'DetectMode()
        'AddHandler bc.StateChange, AddressOf BCh_StateChange
        'AddHandler pc.Attach, AddressOf OnAttachHandler


        'If (pc.Attached) Then
        '    pc.Open()
        '    pc.State = False
        'End If
        'If (bc.Attached) Then
        '    bc.Open(5000)
        'End If

        'If (fc.Attached) Then
        'fc.Open()
        'End If
        Clock()
    End Sub

    Private Sub Clock() Handles timer.Elapsed
        Application.Current.Dispatcher.BeginInvoke(New Action(AddressOf ControlLoop))
    End Sub

  

    Private Sub InitWatches()
        ActiveStimVal.Content = ActiveStimWatch.ElapsedMilliseconds
        PressWatchVal.Content = StimAWatch.ElapsedMilliseconds + StimBWatch.ElapsedMilliseconds
        StimAWatchVal.Content = StimAWatch.ElapsedMilliseconds
        StimBWatchVal.Content = StimBWatch.ElapsedMilliseconds
    End Sub

    Private Sub ControlLoop()
        InitWatches() 'sets values in UI

        If (StimAWatch.ElapsedMilliseconds + StimBWatch.ElapsedMilliseconds >= 10000) Then 'check that button holding time isn't over 100 seconds
            'fc.State = True 'activate feeder for banana pellet if target time met
            LockOut()
            ResetTrial() 'reset the trial values
        End If

        If (ActiveStimWatch.ElapsedMilliseconds >= 1000) Then
            StimGrid.Background = Brushes.Black
            ActiveStimWatch.Stop()
            StimAWatch.Stop()
            StimBWatch.Stop()
        End If

        'Dim prog = PressWatch Mod 10000
        'If (prog.Equals(0)) Then
        '    pc.State = True
        'ElseIf (prog > 0) Then
        '    pc.State = False
        'End If
    End Sub

    Public Sub RecordData()
        'add data to the textbox by pasting the content of all the labels into a comma seperated line of text
        TextBox1.Text = TextBox1.Text &
            SubjectName.Text & " , " &
            btnCount & " , " &
            StimAWatch.ElapsedMilliseconds & " , " &
            StimAWatch.ElapsedMilliseconds & " , " &
            ActiveStimWatch.ElapsedMilliseconds & " , " &
            Latency.ElapsedMilliseconds & " , " &
            Latency.ElapsedMilliseconds / btnCount &
            System.Environment.NewLine

        TextBox1.ScrollToEnd()
    End Sub

    Private Sub LockOut()
        bc.Close() 'prevent button activate 
        System.Threading.Thread.Sleep(30000) '
        bc.Open()
    End Sub

    Private Sub ResetTrial()
        StimGrid.Background = Brushes.Black
        Latency.Stop()
        ActiveStimWatch.Stop()
        StimAWatch.Stop()
        StimBWatch.Stop()
        RecordData()
        PressWatch = 0
        btnCount = 0
        Latency.Reset()
        ActiveStimWatch.Reset()
        StimAWatch.Reset()
        StimBWatch.Reset()
    End Sub

End Class
