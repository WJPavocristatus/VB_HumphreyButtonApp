''' <summary>
''' Manages progress bar animations synchronized to stopwatch values.
''' Provides software-driven feedback independent of hardware state.
''' </summary>
Public Class ProgressBarController
    Private ReadOnly _progressBar As Controls.ProgressBar
    Private ReadOnly _stopwatch As Stopwatch
    Private _maxThreshold As Long ' milliseconds (now mutable)
    Private _isActive As Boolean = False

    Public Sub New(progressBar As Controls.ProgressBar, stopwatch As Stopwatch, maxThresholdMs As Long)
        _progressBar = progressBar
        _stopwatch = stopwatch
        ' Defensive: never set Maximum to 0 (WPF renders Maximum=0 as filled).
        _maxThreshold = If(maxThresholdMs <= 0, 1, maxThresholdMs)
        _progressBar.Maximum = _maxThreshold
        _progressBar.Value = 0
    End Sub

    ''' <summary>
    ''' Update the controller's maximum threshold at runtime.
    ''' Useful when the UI selection (TargetTime) is set after controller construction.
    ''' </summary>
    Public Sub UpdateMaxThreshold(newMaxMs As Long)
        _maxThreshold = If(newMaxMs <= 0, 1, newMaxMs)
        _progressBar.Maximum = _maxThreshold
        If _progressBar.Value > _maxThreshold Then
            _progressBar.Value = _maxThreshold
        End If
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
    ''' Allows external code to set the elapsed milliseconds directly.
    ''' Useful when the progress value is derived from multiple stopwatches.
    ''' </summary>
    Public Sub SetElapsed(elapsedMs As Long)
        If Not _isActive Then
            _progressBar.Value = 0
            Return
        End If

        Dim clampedValue = Math.Min(If(elapsedMs < 0, 0, elapsedMs), _maxThreshold)
        _progressBar.Value = clampedValue
    End Sub

    ''' <summary>
    ''' Activates progress bar tracking.
    ''' </summary>
    Public Sub Activate()
        _isActive = True
    End Sub

    ''' <summary>
    ''' Deactivates progress bar tracking.
    ''' If preserve is True the current filled amount is left visible;
    ''' otherwise the bar is reset to zero.
    ''' </summary>
    Public Sub Deactivate(Optional preserve As Boolean = False)
        _isActive = False
        If Not preserve Then
            _progressBar.Value = 0
        End If
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