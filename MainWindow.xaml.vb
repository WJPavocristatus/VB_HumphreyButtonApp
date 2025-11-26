Imports System.IO
Imports System.Media
Imports System.Windows.Threading

Class MainWindow

    '==== Phidget Object ===='
    Private WithEvents phidgetButton As Phidget22.Devices.DigitalInput.DigitalInput

    '==== Timing ===='
    Private StimWatch As New Stopwatch()
    Private MasterStopWatch As New Stopwatch()
    Private LockoutTimer As DispatcherTimer
    Private ControlLoopTimer As DispatcherTimer

    '==== State Flags ===='
    Private isLockout As Boolean = True
    Private newTrialReady As Boolean = False
    Private startButtonPressed As Boolean = False

    '==== Visual / Audio ===='
    Private chimePlayer As SoundPlayer

    Private overlayPlay As ImageBrush
    Private overlayGrayA As ImageBrush
    Private overlayGrayB As ImageBrush

    '==== File Paths ===='
    Private autosavePath As String = "autosave.txt"

    '============================================================
    '   WINDOW LOAD
    '============================================================
    Private Sub MainWindow_Loaded(sender As Object, e As RoutedEventArgs) Handles MyBase.Loaded
        ' Load Sound
        chimePlayer = New SoundPlayer("Assets/DefaultChime.wav")

        ' Load texture overlays
        overlayPlay = New ImageBrush(New BitmapImage(New Uri("Assets/playbtn.png", UriKind.Relative)))
        overlayGrayA = New ImageBrush(New BitmapImage(New Uri("Assets/stimAtex.png", UriKind.Relative)))
        overlayGrayB = New ImageBrush(New BitmapImage(New Uri("Assets/stimBtex.png", UriKind.Relative)))

        ' Initialize phidget
        phidgetButton = New Phidget22.Devices.DigitalInput.DigitalInput() With {.Channel = 0}
        phidgetButton.Open(5000)

        ' Lockout timer for animations
        LockoutTimer = New DispatcherTimer() With {.Interval = TimeSpan.FromMilliseconds(100)}

        ' Control loop
        ControlLoopTimer = New DispatcherTimer() With {.Interval = TimeSpan.FromMilliseconds(30)}
        AddHandler ControlLoopTimer.Tick, AddressOf ControlLoop
        ControlLoopTimer.Start()

        ' Initial UI
        StimGrid.Background = Brushes.DarkGray
        StimSpy.Background = overlayPlay
    End Sub

    '============================================================
    '   START BUTTON CLICK
    '============================================================
    Private Sub StartButton_Click(sender As Object, e As RoutedEventArgs)
        startButtonPressed = True
        isLockout = False
        newTrialReady = True   ' allow very first trial

        StimGrid.Background = Brushes.LightGray
        StimSpy.Background = Nothing
    End Sub

    '============================================================
    '   PHIDGET BUTTON EVENT
    '============================================================
    Private Sub phidgetButton_StateChanged(sender As Object, e As Phidget22.Events.DigitalInputStateChangedEventArgs) Handles phidgetButton.StateChanged
        If e.State = True Then
            ' Button pressed – begin trial timing ONLY if allowed
            If Not isLockout AndAlso newTrialReady Then
                If Not StimWatch.IsRunning Then
                    StimWatch.Start()
                End If
            End If
        Else
            ' Button released
            If Not isLockout Then
                newTrialReady = True   ' button released → allow next trial
            End If

            StimWatch.Stop()
            StimWatch.Reset()
        End If
    End Sub

    '============================================================
    '   CONTROL LOOP (Runs every 30ms)
    '============================================================
    Private Sub ControlLoop(sender As Object, e As EventArgs)
        If Not startButtonPressed Then
            ' Pre-start mode behaves like lockout WITHOUT feedback
            Return
        End If

        If isLockout Then
            Return
        End If

        Dim targetTime As Integer = CInt(TargetTimeUpDown.Value)

        '========================================================
        '   TRIAL STARTS WHEN BUTTON PRESSED & newTrialReady True
        '========================================================
        If phidgetButton.State = True AndAlso StimWatch.ElapsedMilliseconds = 0 AndAlso newTrialReady Then

            newTrialReady = False

            ' === Start NEW TRIAL ===
            ' Reset & start master stopwatch HERE
            MasterStopWatch.Reset()
            MasterStopWatch.Start()

            ' Visual Stimulus
            StimGrid.Background = If(Rnd() < 0.5, Brushes.DarkGray, Brushes.LightGray)
            StimSpy.Background = If(StimGrid.Background Is Brushes.DarkGray, overlayGrayA, overlayGrayB)
        End If

        '========================================================
        '   TARGET TIME REACHED → END TRIAL
        '========================================================
        If StimWatch.IsRunning AndAlso StimWatch.ElapsedMilliseconds >= targetTime Then

            ' Stop both timers exactly at the moment trial ends
            StimWatch.Stop()
            MasterStopWatch.Stop()

            ' Play stimulus-reached chime
            chimePlayer.Play()

            ' Begin lockout animation cycle
            isLockout = True
            StartLockoutAnimation()

            Return
        End If

    End Sub

    '============================================================
    '   LOCKOUT ANIMATION
    '============================================================
    Private Sub StartLockoutAnimation()
        Dim stepCount As Integer = 0
        AddHandler LockoutTimer.Tick,
            Sub()
                stepCount += 1

                ' Swap backgrounds one time
                If stepCount = 1 Then
                    StimGrid.Background = Brushes.DarkGray
                    StimSpy.Background = overlayGrayA
                ElseIf stepCount = 2 Then
                    StimGrid.Background = Brushes.LightGray
                    StimSpy.Background = overlayGrayB
                Else
                    ' === End of lockout ===
                    LockoutTimer.Stop()

                    ' Reset master stopwatch for next trial
                    MasterStopWatch.Reset()

                    isLockout = False
                    ' DO NOT set newTrialReady here – requires button release
                End If
            End Sub

        LockoutTimer.Start()
    End Sub

    '============================================================
    '   AUTOSAVE ON CLOSE
    '============================================================
    Private Sub Window_Closing(sender As Object, e As ComponentModel.CancelEventArgs)
        Try
            File.WriteAllText(autosavePath, "LastRun=" & DateTime.Now.ToString())
        Catch
        End Try
    End Sub

End Class
