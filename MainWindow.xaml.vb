Imports System.Diagnostics
Imports System.Reflection.Emit
Imports System.Windows
Imports System.Windows.Media
Imports Phidget22
Imports Phidget22.Events

Public Class MainWindow
    'Inherits Window

    Private bc As DigitalInput = New DigitalInput()
    Friend WithEvents timer As New System.Timers.Timer
    Public btnCount As Integer = 0

    Public Property PressWatch As Long
    Public Property ActiveStimWatch As Stopwatch = New Stopwatch()
    Public Property StimAWatch As Stopwatch = New Stopwatch()
    Public Property StimBWatch As Stopwatch = New Stopwatch()

    Public Sub New()
        bc.DeviceSerialNumber = 705800
        bc.Channel = 13
        timer.Start()
        timer.Interval = 1
        Log.Enable(LogLevel.Info, "file.log")

        AddHandler bc.Attach, AddressOf OnAttachHandler
        AddHandler bc.StateChange, AddressOf BCh_StateChange

        bc.Open()
        Clock()
    End Sub

    Private Sub Clock() Handles timer.Elapsed
        Application.Current.Dispatcher.BeginInvoke(New Action(AddressOf controlloop))
    End Sub

    Private Sub controlloop()
        ActiveStimVal.Content = ActiveStimWatch.ElapsedMilliseconds
        PressWatchVal.Content = StimAWatch.ElapsedMilliseconds + StimBWatch.ElapsedMilliseconds
        StimAWatchVal.Content = StimAWatch.ElapsedMilliseconds
        StimBWatchVal.Content = StimBWatch.ElapsedMilliseconds
        If (StimAWatch.ElapsedMilliseconds + StimBWatch.ElapsedMilliseconds >= 60000) Then
            ResetTrial()
        End If
        If (ActiveStimWatch.ElapsedMilliseconds >= 10000) Then
            StimGrid.Background = Brushes.Black
            ActiveStimWatch.Stop()
            StimAWatch.Stop()
            StimBWatch.Stop()
        End If
    End Sub

    Private Sub SetGridColor(count As Integer)
        Select Case (count Mod 2)
            Case 0
                StimGrid.Background = Brushes.Blue
                ActiveStimWatch.Start()
                StimAWatch.Start()
            Case 1
                StimGrid.Background = Brushes.Orange
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
        ActiveStimWatch.Reset()
        StimAWatch.Reset()
        StimBWatch.Reset()
    End Sub

    ' Clean up the Phidget resources when the application closes
    Protected Overrides Sub OnClosed(e As EventArgs)
        MyBase.OnClosed(e)
        If bc IsNot Nothing Then
            bc.Close()
        End If
    End Sub
End Class
