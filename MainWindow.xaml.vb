Imports System.Diagnostics
Imports System.Windows
Imports System.Windows.Media
Imports Phidget22
Imports Phidget22.Events

Public Class MainWindow
    'Inherits Window

    Private bc As DigitalInput
    'Public bg As Controls.Grid.Name.Equals("StimGrid")
    Public btnCount As Integer = 0

    Public Property pressTimer As Stopwatch = New Stopwatch()
    Public Property activeStimulus As Stopwatch = New Stopwatch()
    Public Property stimATimer As Stopwatch = New Stopwatch()
    Public Property stimBTimer As Stopwatch = New Stopwatch()

    Private Sub Initiate() Handles MyBase.Loaded

        'Me.DataContext = Me

        bc = New DigitalInput()


        bc.DeviceSerialNumber = 705800
        bc.Channel = 13



        AddHandler bc.Attach, AddressOf OnAttachHandler
        AddHandler bc.StateChange, AddressOf BCh_StateChange

        bc.Open()
    End Sub

    Private Sub BCh_StateChange(sender As Object, e As DigitalInputStateChangeEventArgs)
        Dispatcher.Invoke(Sub()
                              If e.State Then
                                  btnCount += 1

                                  Select Case (btnCount Mod 2)
                                      Case 0
                                          StimGrid.Background = Brushes.Blue
                                          stimATimer.Start()
                                      Case 1
                                          StimGrid.Background = Brushes.Orange
                                          stimBTimer.Start()
                                  End Select
                              Else
                                  activeStimulus.Stop()
                                  stimATimer.Stop()
                                  stimBTimer.Stop()
                                  StimGrid.Background = Brushes.Black
                              End If
                          End Sub)
    End Sub

    Private Sub OnAttachHandler(sender As Object, e As AttachEventArgs)
        Dispatcher.Invoke(Sub()
                              Initiate()
                          End Sub)
    End Sub

    ' Reset Stopwatches to 0 for new trial after pressTimer reaches 1 minute
    Private Sub ResetTrial()
        pressTimer.Reset()
    End Sub

    ' Clean up the Phidget resources when the application closes
    Protected Overrides Sub OnClosed(e As EventArgs)
        MyBase.OnClosed(e)
        If bc IsNot Nothing Then
            bc.Close()
        End If
    End Sub
End Class
