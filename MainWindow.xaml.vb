Imports System.Diagnostics
Imports System.Reflection.Emit
Imports System.Windows
Imports System.Windows.Media
Imports Phidget22
Imports Phidget22.Events

Public Class MainWindow


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

        InitializeComponent()



        bc.DeviceSerialNumber = 705800
        'fc.DeviceSerialNumber = 705800
        bc.Channel = 0
        'fc.Channel = 15
        'pc.DeviceSerialNumber = 705800
        'pc.Channel = 2
        timer.Start()
        timer.Interval = 1
        Log.Enable(LogLevel.Info, "file.log")

        AddHandler bc.Attach, AddressOf OnAttachHandler
        'AddHandler fc.Attach, AddressOf OnAttachHandler

        AddHandler bc.StateChange, AddressOf BCh_StateChange
        'AddHandler pc.Attach, AddressOf OnAttachHandler

        'If (pc.Attached) Then
        '    pc.Open()
        '    pc.State = False
        'End If
        bc.Open()

        'If (fc.Attached) Then
        'fc.Open()
        'End If
        Clock()
    End Sub

    Private Sub Clock() Handles timer.Elapsed
        Application.Current.Dispatcher.BeginInvoke(New Action(AddressOf controlloop))
    End Sub

    Private Sub controlloop()


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



    Private Sub SetGridColor(count As Integer)
        Select Case (count Mod 2)
            Case 0
                ActiveStimWatch.Start()
                StimAWatch.Start()
                StimGrid.Background = Brushes.White
            Case 1
                ActiveStimWatch.Start()
                StimBWatch.Start()
                StimGrid.Background = Brushes.Red
        End Select
    End Sub

    Private Sub BCh_StateChange(sender As Object, e As DigitalInputStateChangeEventArgs)
        Dispatcher.Invoke(Sub()
                              If e.State Then
                                  Latency.Stop()
                                  If (ActiveStimWatch.ElapsedMilliseconds <= 10000) Then
                                      btnCount = btnCount + 1
                                      ActiveStimWatch.Reset()
                                      SetGridColor(btnCount)
                                  ElseIf (ActiveStimWatch.ElapsedMilliseconds >= 10000) Then
                                      ActiveStimWatch.Reset()
                                      StimAWatch.Reset()
                                      StimBWatch.Reset()
                                  End If
                              Else
                                  Latency.Start()
                                  ActiveStimWatch.Stop()
                                  StimAWatch.Stop()
                                  StimBWatch.Stop()
                                  StimGrid.Background = Brushes.Black
                                  'RecordData()
                              End If
                          End Sub)
    End Sub

    Private Sub Virtual_StateChange(sender As Object, e As RoutedEventArgs)
        Dispatcher.Invoke(Sub()
                              If e.Handled Then
                                  Latency.Stop()
                                  If (ActiveStimWatch.ElapsedMilliseconds <= 10000) Then
                                      btnCount = btnCount + 1
                                      ActiveStimWatch.Reset()
                                      SetGridColor(btnCount)
                                  ElseIf (ActiveStimWatch.ElapsedMilliseconds >= 10000) Then
                                      ActiveStimWatch.Reset()
                                      StimAWatch.Reset()
                                      StimBWatch.Reset()
                                  End If
                              Else
                                  Latency.Start()
                                  ActiveStimWatch.Stop()
                                  StimAWatch.Stop()
                                  StimBWatch.Stop()
                                  StimGrid.Background = Brushes.Black
                                  'RecordData()
                              End If

                          End Sub)
    End Sub

    Private Sub OnAttachHandler(sender As Object, e As AttachEventArgs)
        Dispatcher.Invoke(Sub()
                              Log.WriteLine(LogLevel.Info, "Phidget button attached!")
                          End Sub)
    End Sub

    Private Sub LockOut()
        bc.Close() 'prevent button activate 
        System.Threading.Thread.Sleep(30000) '
        bc.Open()
    End Sub

    ' Reset Stopwatches to 0 for new trial after pressTimer reaches 100 seconds
    Private Sub ResetTrial()
        StimGrid.Background = Brushes.Black
        Latency.Stop()
        ActiveStimWatch.Stop()
        StimAWatch.Stop()
        StimBWatch.Stop()
        'ResearcherWindow.
        'ResearcherWindow.RecordData()
        PressWatch = 0
        btnCount = 0
        Latency.Reset()
        ActiveStimWatch.Reset()
        StimAWatch.Reset()
        StimBWatch.Reset()
    End Sub







    ' Clean up the Phidget resources when the application closes
    Protected Overrides Sub OnClosed(e As EventArgs)
        If bc IsNot Nothing Then
            bc.Close()
        End If
        'If fc IsNot Nothing Then
        '    fc.Close()
        'End If
        MyBase.OnClosed(e)
    End Sub
End Class
