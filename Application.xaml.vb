Imports Phidget22

Class Application
    Public Shared Property opMode As String
    Private ph As New DigitalInput()
    'Private pc As New DigitalOutput() '<-- INTENDED FOR PHYSICAL (I.E., LED) PROGRESS BA; pc = "Progress Channel"
    'Private bc As New DigitalInput() 'bc = "Button Channel"
    'Private fc As New DigitalOutput() 'fc = "Feeder Channel"


    Public Sub New()
        ph.DeviceSerialNumber = 705800
        ph.Channel = 1
        Try
            ph.Open(3000)
            opMode = "attached"
        Catch ex As PhidgetException
            MessageBox.Show($"{ex.Message} Entering virtual testing mode!")
            opMode = "virtual"
        End Try
    End Sub

    Private Sub App_Startup(sender As Object, e As StartupEventArgs)
        Dim cw As New ResearcherWindow()
        cw.Show() ' Show the second window
    End Sub

    Public Sub DetectMode()

        If opMode Is "attached" Then
            MainWindow.FindName("VirtualBtn").Visibility = Visibility.Hidden
        ElseIf opMode Is "virtual" Then
            MainWindow.FindName("VirtualBtn").Visibility = Visibility.Visible
        End If
    End Sub
End Class
