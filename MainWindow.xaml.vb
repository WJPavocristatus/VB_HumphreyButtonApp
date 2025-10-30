﻿Imports Phidget22
Imports Phidget22.Events

Public Class MainWindow
    Friend WithEvents timer As New System.Timers.Timer

    Private bc As New DigitalInput() 'bc = "Button Channel"
    Private cc As New DigitalOutput() 'clicker channel
    Private fc As New DigitalOutput() 'fc = "Feeder Channel"
    Private flc As New DigitalOutput() 'flc = "Feeder LED Channel"
    Private llc As New DigitalOutput() 'llc = "Lockout LED Channel"
    'Private pc As New DigitalOutput() '<-- INTENDED FOR PHYSICAL (I.E., LED) PROGRESS BA; pc = "Progress Channel"

    Public btnCount As Integer = 0

    Public Property PressWatch As Long
    Public Property Latency As Stopwatch = New Stopwatch()
    Public Property ActiveStimWatch As Stopwatch = New Stopwatch()
    Public Property StimAWatch As Stopwatch = New Stopwatch()
    Public Property StimBWatch As Stopwatch = New Stopwatch()

    Public Sub New()
        bc.DeviceSerialNumber = 705800
        cc.DeviceSerialNumber = 705800
        fc.DeviceSerialNumber = 705800
        flc.DeviceSerialNumber = 705800
        llc.DeviceSerialNumber = 705800
        'pc.DeviceSerialNumber = 705800
        cc.Channel = 15
        bc.Channel = 0
        fc.Channel = 7
        flc.Channel = 0
        llc.Channel = 8
        'pc.Channel = 2
        Log.Enable(LogLevel.Info, "file.log")

        AddHandler bc.Attach, AddressOf OnAttachHandler
        AddHandler fc.Attach, AddressOf OnAttachHandler
        AddHandler cc.Attach, AddressOf OnAttachHandler
        AddHandler bc.StateChange, AddressOf BCh_StateChange
        'AddHandler pc.Attach, AddressOf OnAttachHandler

        'If (pc.Attached) Then
        '    pc.Open()
        '    pc.State = False
        'End If
        cc.Open()
        bc.Open()
        'If (fc.Attached) Then
        fc.Open()
        flc.Open()
        llc.Open()
        'End If
        timer.Start()
        timer.Interval = 1
        Clock()
    End Sub

    Private Sub InitMainWindow() Handles MyBase.Initialized
        MainWin.Width = SystemParameters.PrimaryScreenWidth * 2
        MainWin.Height = SystemParameters.PrimaryScreenHeight
        MainWin.Top = 0
        MainWin.Left = 0
        MainWin.WindowStyle = WindowStyle.None
        MainWin.ResizeMode = ResizeMode.NoResize
    End Sub

    Private Sub Clock() Handles timer.Elapsed
        Application.Current.Dispatcher.BeginInvoke(New Action(AddressOf controlloop))
    End Sub

    Private Sub controlloop()
        InitWatches() 'sets values in UI

        If (StimAWatch.ElapsedMilliseconds + StimBWatch.ElapsedMilliseconds >= 10000) Then 'check that button holding time isn't over 100 seconds
            LockOut()
            ResetTrial() 'reset the trial values
        End If

        If (ActiveStimWatch.ElapsedMilliseconds >= 10000) Then
            StimGrid.Background = Brushes.Black
            StimSpy.Background = Brushes.Black
            ActiveStimWatch.Stop()
            StimAWatch.Stop()
            StimBWatch.Stop()
        End If
    End Sub

    Private Sub InitWatches()
        PressWatchVal.Content = $"{(StimAWatch.ElapsedMilliseconds + StimBWatch.ElapsedMilliseconds) * 1000}"
        ActiveStimVal.Content = $"{ActiveStimWatch.ElapsedMilliseconds * 1000} secs"
        StimAWatchVal.Content = $"{StimAWatch.ElapsedMilliseconds * 1000} secs"
        StimBWatchVal.Content = $"{StimBWatch.ElapsedMilliseconds * 1000} secs"
        Latency.Reset()
        Latency.Stop()
        LatencyVal.Content = $"{Latency.ElapsedMilliseconds} secs"
    End Sub

    Private Sub SetGridColor(count As Integer)
        Select Case (count Mod 2)
            Case 0
                ActiveStimWatch.Start()
                StimAWatch.Start()
                StimGrid.Background = Brushes.White
                StimSpy.Background = Brushes.White
            Case 1
                ActiveStimWatch.Start()
                StimBWatch.Start()
                StimGrid.Background = Brushes.Red
                StimSpy.Background = Brushes.Red
        End Select
    End Sub

    Private Sub BCh_StateChange(sender As Object, e As DigitalInputStateChangeEventArgs)
        Dispatcher.Invoke(Sub()
                              If e.State Then
                                  ActivateOut(cc, 35)
                                  Latency.Stop()
                                  If (ActiveStimWatch.ElapsedMilliseconds <= 10000) Then
                                      btnCount = btnCount + 1
                                      ActiveStimWatch.Reset()
                                      SetGridColor(btnCount)
                                  ElseIf (ActiveStimWatch.ElapsedMilliseconds >= 10000) Then
                                      ActiveStimWatch.Reset()
                                      StimAWatch.Reset()
                                      StimBWatch.Reset()
                                      StimGrid.Background = Brushes.Black
                                      StimSpy.Background = Brushes.Black
                                  End If
                              Else
                                  Latency.Start()
                                  ActiveStimWatch.Stop()
                                  StimAWatch.Stop()
                                  StimBWatch.Stop()
                                  StimGrid.Background = Brushes.Black
                                  StimSpy.Background = Brushes.Black
                                  RecordData()
                              End If

                          End Sub)
    End Sub

    Private Sub OnAttachHandler(sender As Object, e As AttachEventArgs)
        Dispatcher.Invoke(Sub()
                              Log.WriteLine(LogLevel.Info, $"Phidget {sender} attached!")
                              Console.WriteLine($"Phidget {sender} attached!")
                          End Sub)
    End Sub

    Private Sub LockOut() 'gross AF pattern, should fix in the future
        StimGrid.Background = Brushes.Black
        StimSpy.Background = Brushes.Black
        bc.Close() 'prevent button activate
        LockedLED()
        FeederLED()
        ActivateOut(fc, 50)
        FeederLED()
        System.Threading.Thread.Sleep(30000) 'really oughta do multi-threading in next iteration
        bc.Open()
        LockedLED()
    End Sub

    Private Sub ActivateOut(chan As DigitalOutput, ms As Integer)
        chan.State = True
        System.Threading.Thread.Sleep(ms)
        chan.State = False
    End Sub

    Private Sub FeederLED()
        If flc.State = False Then
            flc.State = True
        Else
            flc.State = False
        End If
    End Sub

    Private Sub LockedLED()
        If llc.State = False Then
            llc.State = True
        Else
            llc.State = False
        End If
    End Sub

    ' Reset Stopwatches to 0 for new trial after pressTimer reaches 100 seconds
    Private Sub ResetTrial()
        StimGrid.Background = Brushes.Black
        StimSpy.Background = Brushes.Black
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
        LockOut()
    End Sub

    Private Sub RecordData()
        'add data to the textbox by pasting the content of all the labels into a comma seperated line of text
        TextBox1.Text = TextBox1.Text &
            $"{SubjectName}, " &
            $"Button Presses: {btnCount}, " &
            $"Press duration: {ActiveStimWatch.ElapsedMilliseconds * 1000} secs, " &
            $"Total StimA Watch Time: {StimAWatch.ElapsedMilliseconds * 1000} secs, " &
            $"Total StimB Watch Time: {StimBWatch.ElapsedMilliseconds * 1000} secs, " &
            $"Latency time: {Latency.ElapsedMilliseconds * 1000} sec, " &
            $"Avg. Hold Time: {(Latency.ElapsedMilliseconds * 1000) / btnCount} secs" &
            System.Environment.NewLine

        TextBox1.ScrollToEnd()
    End Sub

    Private Sub Save_Click(sender As Object, e As RoutedEventArgs) Handles BtnSave.Click
        'opens up a save file dialogue to save the content of the textbox to a .txt file
        Dim SaveFileDialog1 As New Microsoft.Win32.SaveFileDialog
        SaveFileDialog1.FileName = $"{SubjectName.Text}_{System.DateTime.Now.ToFileTimeUtc}.csv"
        SaveFileDialog1.DefaultExt = ".csv"
        SaveFileDialog1.ShowDialog()

        If SaveFileDialog1.FileName <> "" Then
            System.IO.File.WriteAllText(SaveFileDialog1.FileName, TextBox1.Text)
        End If
    End Sub

    ' Clean up the Phidget resources when the application closes
    Protected Overrides Sub OnClosed(e As EventArgs)
        bc?.Close()
        cc?.Close()
        fc?.Close()
        flc?.Close()
        llc?.Close()
        MyBase.OnClosed(e)
    End Sub
End Class
