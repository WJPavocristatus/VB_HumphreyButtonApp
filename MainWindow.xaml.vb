Imports System.Diagnostics
Imports System.Reflection.Emit
Imports System.Windows
Imports System.Windows.Media
Imports Phidget22
Imports Phidget22.Events

Public Class MainWindow

    Private bc As New DigitalInput()
    'Private pc As New DigitalOutput()
    Private fc As New DigitalOutput()
    Friend WithEvents timer As New System.Timers.Timer
    Public btnCount As Integer = 0

    Public Property PressWatch As Long
    Public Property ActiveStimWatch As Stopwatch = New Stopwatch()
    Public Property StimAWatch As Stopwatch = New Stopwatch()
    Public Property StimBWatch As Stopwatch = New Stopwatch()

    Public Sub New()
        bc.DeviceSerialNumber = 705800
        'pc.DeviceSerialNumber = 705800
        fc.DeviceSerialNumber = 705800
        bc.Channel = 13
        fc.Channel = 15
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
        fc.Open()
        'End If
        Clock()
    End Sub

    Private Sub Clock() Handles timer.Elapsed
        Application.Current.Dispatcher.BeginInvoke(New Action(AddressOf controlloop))
    End Sub

    Private Sub controlloop()
        InitWatches()

        If (StimAWatch.ElapsedMilliseconds + StimBWatch.ElapsedMilliseconds >= 5000) Then
            fc.State = True
            ResetTrial()
        End If

        If (ActiveStimWatch.ElapsedMilliseconds >= 10000) Then
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

    Private Sub InitWatches()
        ActiveStimVal.Content = ActiveStimWatch.ElapsedMilliseconds
        PressWatchVal.Content = StimAWatch.ElapsedMilliseconds + StimBWatch.ElapsedMilliseconds
        StimAWatchVal.Content = StimAWatch.ElapsedMilliseconds
        StimBWatchVal.Content = StimBWatch.ElapsedMilliseconds
    End Sub

    Private Sub SetGridColor(count As Integer)
        Select Case (count Mod 2)
            Case 0
                StimGrid.Background = Brushes.White
                ActiveStimWatch.Start()
                StimAWatch.Start()
            Case 1
                StimGrid.Background = Brushes.Red
                ActiveStimWatch.Start()
                StimBWatch.Start()
        End Select
    End Sub

    Private Sub BCh_StateChange(sender As Object, e As DigitalInputStateChangeEventArgs)
        Dispatcher.Invoke(Sub()
                              If e.State Then
                                  If (ActiveStimWatch.ElapsedMilliseconds <= 10000) Then
                                      btnCount = btnCount + 1
                                      ActiveStimWatch.Reset()
                                      SetGridColor(btnCount)
                                  ElseIf (ActiveStimWatch.ElapsedMilliseconds > 10000) Then
                                      ActiveStimWatch.Reset()
                                      StimAWatch.Reset()
                                      StimBWatch.Reset()
                                  End If
                              Else
                                  ActiveStimWatch.Stop()
                                  StimAWatch.Stop()
                                  StimBWatch.Stop()
                                  StimGrid.Background = Brushes.Black
                                  recorddata()
                              End If
                          End Sub)
    End Sub

    Private Sub OnAttachHandler(sender As Object, e As AttachEventArgs)
        Dispatcher.Invoke(Sub()
                              Log.WriteLine(LogLevel.Info, "Phidget button attached!")
                          End Sub)

    End Sub

    ' Reset Stopwatches to 0 for new trial after pressTimer reaches 1 minute
    Private Sub ResetTrial()
        btnCount = 0
        PressWatch = 0
        StimGrid.Background = Brushes.Black
        ActiveStimWatch.Stop()
        StimAWatch.Stop()
        StimBWatch.Stop()
        recorddata()
        ActiveStimWatch.Reset()
        StimAWatch.Reset()
        StimBWatch.Reset()
    End Sub

    Private Sub recorddata()
        'add data to the textbox by pasting the content of all the labels into a comma seperated line of text
        TextBox1.Text = TextBox1.Text & SubjectName.Text & " , " & btnCount & " , " & StimAWatch.ElapsedMilliseconds & " , " & StimAWatch.ElapsedMilliseconds & " , " & ActiveStimWatch.ElapsedMilliseconds & System.Environment.NewLine


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
        MyBase.OnClosed(e)
        If bc IsNot Nothing Then
            bc.Close()
        End If
    End Sub
End Class
