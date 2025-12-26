Imports System.Diagnostics
Imports System.Threading.Tasks

''' <summary>
''' Manages stopwatches, target-time logic and trial completion detection.
''' UI / MainWindow should call ConfirmPress/ConfirmRelease after debounce.
''' Raises TrialCompleted when totalPress >= TargetTime.
''' </summary>
Public Class SessionController
    Public ReadOnly ActiveStimWatch As New Stopwatch()
    Public ReadOnly StimAWatch As New Stopwatch()
    Public ReadOnly StimBWatch As New Stopwatch()
    Public ReadOnly MasterWatch As New Stopwatch()

    Public Property TargetTimeMs As Integer = 3000
    Public Property HoldLimitMs As Integer = 5000

    Public Event TrialCompleted As EventHandler

    Public Sub StartSession(targetMs As Integer)
        TargetTimeMs = targetMs
        MasterWatch.Restart()
        ResetWatches()
    End Sub

    Public Sub StopSession()
        MasterWatch.Stop()
        ResetWatches()
    End Sub

    Public Sub ResetAllWatches()
        MasterWatch.Reset()
        ResetWatches()
    End Sub

    Private Sub ResetWatches()
        ActiveStimWatch.Reset()
        StimAWatch.Reset()
        StimBWatch.Reset()
    End Sub

    ''' <summary>
    ''' Call when a stable button press is confirmed (UI thread).
    ''' Starts the active stimulus watch and returns true if holdlimit allows continuing.
    ''' </summary>
    Public Function PressConfirmed() As Boolean
        If ActiveStimWatch.ElapsedMilliseconds >= HoldLimitMs Then
            Return False
        End If

        If Not ActiveStimWatch.IsRunning Then ActiveStimWatch.Start()
        Return True
    End Function

    ''' <summary>
    ''' Call when a stable release is confirmed (UI thread). This method checks
    ''' if the cumulative stimulus time reached the TargetTime and raises TrialCompleted.
    ''' </summary>
    Public Sub ReleaseConfirmed()
        If ActiveStimWatch.IsRunning Then ActiveStimWatch.Stop()
        Dim totalPress = StimAWatch.ElapsedMilliseconds + StimBWatch.ElapsedMilliseconds

        If totalPress >= TargetTimeMs Then
            RaiseEvent TrialCompleted(Me, EventArgs.Empty)
        End If
    End Sub

    Public Sub StartStimA()
        If Not StimAWatch.IsRunning Then StimAWatch.Start()
    End Sub

    Public Sub StopStimA()
        If StimAWatch.IsRunning Then StimAWatch.Stop()
    End Sub

    Public Sub StartStimB()
        If Not StimBWatch.IsRunning Then StimBWatch.Start()
    End Sub

    Public Sub StopStimB()
        If StimBWatch.IsRunning Then StimBWatch.Stop()
    End Sub
End Class