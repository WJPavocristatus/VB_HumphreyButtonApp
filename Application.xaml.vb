Class Application

    ' Application-level events, such as Startup, Exit, and DispatcherUnhandledException
    ' can be handled in this file.

    Private Sub App_Startup(sender As Object, e As StartupEventArgs)
        Dim cw As New ChimpWindow()
        cw.Left = 400 ' Set position
        cw.Top = 200
        cw.Show() ' Show the second window
    End Sub
End Class
