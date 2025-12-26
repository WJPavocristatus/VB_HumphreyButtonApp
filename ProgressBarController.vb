''' <summary>
''' Manages progress bar animations synchronized to stopwatch values.
''' Provides software-driven feedback independent of hardware state.
''' </summary>
Public Class ProgressBarController
    Private ReadOnly _progressBar As Controls.ProgressBar
    Private ReadOnly _stopwatch As Stopwatch
    Private ReadOnly _maxThreshold As Long ' milliseconds
    Private _isActive As Boolean = False

    Public Sub New(progressBar As Controls.ProgressBar, stopwatch As Stopwatch, maxThresholdMs As Long)
        _progressBar = progressBar
        _stopwatch = stopwatch
        _maxThreshold = maxThresholdMs
        _progressBar.Maximum = maxThresholdMs
        _progressBar.Value = 0
    End Sub

    ''' <summary>
    ''' Updates progress bar based on current stopwatch elapsed time.
    ''' Call this frequently (e.g., from UI timer).
    ''' </summary>
    Public Sub Update()
        If Not _isActive OrElse _stopwatch Is Nothing Then
            _progressBar.Value = 0
            Return
        End If

        Dim elapsed = _stopwatch.ElapsedMilliseconds
        Dim clampedValue = Math.Min(elapsed, _maxThreshold)
        _progressBar.Value = clampedValue
    End Sub

    ''' <summary>
    ''' Activates progress bar tracking.
    ''' </summary>
    Public Sub Activate()
        _isActive = True
    End Sub

    ''' <summary>
    ''' Deactivates progress bar tracking and resets to zero.
    ''' </summary>
    Public Sub Deactivate()
        _isActive = False
        _progressBar.Value = 0
    End Sub

    ''' <summary>
    ''' Gets the current progress as a percentage (0-100).
    ''' </summary>
    Public Function GetProgressPercentage() As Double
        If _maxThreshold <= 0 Then Return 0
        Return (_progressBar.Value / _maxThreshold) * 100
    End Function

    ''' <summary>
    ''' Checks if progress has reached or exceeded the threshold.
    ''' </summary>
    Public Function IsThresholdReached() As Boolean
        Return _progressBar.Value >= _maxThreshold
    End Function

    ''' <summary>
    ''' Resets progress bar to zero without deactivating.
    ''' </summary>
    Public Sub Reset()
        _progressBar.Value = 0
    End Sub
End Class