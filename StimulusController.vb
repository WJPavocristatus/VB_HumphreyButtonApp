Imports System.Windows.Controls
Imports System.Windows.Media
Imports System.Windows.Media.Imaging
Imports System.Diagnostics

''' <summary>
''' Responsible for rendering stimulus visuals and starting per-color watches.
''' Construct with UI elements and the watches the app uses.
''' Methods are safe to call from the UI thread.
''' </summary>
Public Class StimulusController
    Private ReadOnly _stimGrid As Grid
    Private ReadOnly _overlay As Image
    Private ReadOnly _readyOverlay As Image

    Private ReadOnly _blueWatch As Stopwatch
    Private ReadOnly _greenWatch As Stopwatch
    Private ReadOnly _yellowWatch As Stopwatch
    Private ReadOnly _orangeWatch As Stopwatch
    Private ReadOnly _redWatch As Stopwatch

    Public Sub New(stimGrid As Grid,
                   overlay As Image,
                   readyOverlay As Image,
                   blueWatch As Stopwatch,
                   greenWatch As Stopwatch,
                   yellowWatch As Stopwatch,
                   orangeWatch As Stopwatch,
                   redWatch As Stopwatch)
        _stimGrid = stimGrid
        _overlay = overlay
        _readyOverlay = readyOverlay

        _blueWatch = blueWatch
        _greenWatch = greenWatch
        _yellowWatch = yellowWatch
        _orangeWatch = orangeWatch
        _redWatch = redWatch
    End Sub

    Public Sub ResetGridVisuals()
        _stimGrid.Background = Brushes.Black
        _overlay.Visibility = Visibility.Collapsed
    End Sub

    Public Sub ShowReadyIndicator()
        _readyOverlay.Source = New BitmapImage(New Uri("Assets/playbtn.png", UriKind.Relative))
        _readyOverlay.Visibility = Visibility.Visible
    End Sub

    Public Sub HideReadyIndicator()
        _readyOverlay.Visibility = Visibility.Collapsed
    End Sub

    Public Sub ShowOverlay(image As Image, file As String)
        image.Source = New BitmapImage(New Uri(file, UriKind.Relative))
        image.Visibility = Visibility.Visible
    End Sub

    Public Sub SetGridColor_TRAINING(count As Integer)
        ' Training mode: alternate simplified visuals
        If count Mod 2 = 0 Then
            If _blueWatch.IsRunning Then _blueWatch.Stop()
            If Not _orangeWatch.IsRunning Then _orangeWatch.Start()
            _stimGrid.Background = Brushes.Gray
            ShowOverlay(_overlay, "Assets/invert_hd-wallpaper-7939241_1280.png")
        Else
            If _orangeWatch.IsRunning Then _orangeWatch.Stop()
            If Not _blueWatch.IsRunning Then _blueWatch.Start()
            _stimGrid.Background = Brushes.LightGray
            ShowOverlay(_overlay, "Assets/waves-9954690_1280.png")
        End If
    End Sub

    Public Sub SetGridColor_SEQUENCE(idx As Integer, stimSeq As Object)
        ' stimSeq expected to have Color1..Color5 (same as StimulusSequence)
        ' idx is the stimulus step index (0..9)
        Select Case idx
            Case 0
                StartStimA(stimSeq.Color1)
            Case 1
                StartStimBWhite()
            Case 2
                StartStimA(stimSeq.Color2)
            Case 3
                StartStimBWhite()
            Case 4
                StartStimA(stimSeq.Color3)
            Case 5
                StartStimBWhite()
            Case 6
                StartStimA(stimSeq.Color4)
            Case 7
                StartStimBWhite()
            Case 8
                StartStimA(stimSeq.Color5)
            Case 9
                StartStimBWhite()
        End Select
    End Sub

    Private Sub StartStimA(brush As SolidColorBrush)
        ' Stim A color
        RunColorWatch(brush)
        _stimGrid.Background = brush
    End Sub

    Private Sub StartStimBWhite()
        _stimGrid.Background = Brushes.White
    End Sub

    Public Sub RunColorWatch(brush As SolidColorBrush)
        Dim c = brush.Color
        If c = Brushes.Blue.Color Then
            If Not _blueWatch.IsRunning Then _blueWatch.Start()
        ElseIf c = Brushes.Green.Color Then
            If Not _greenWatch.IsRunning Then _greenWatch.Start()
        ElseIf c = Brushes.Yellow.Color Then
            If Not _yellowWatch.IsRunning Then _yellowWatch.Start()
        ElseIf c = Brushes.Orange.Color Then
            If Not _orangeWatch.IsRunning Then _orangeWatch.Start()
        ElseIf c = Brushes.Red.Color Then
            If Not _redWatch.IsRunning Then _redWatch.Start()
        End If
    End Sub

    Public Sub EndColorWatch()
        _blueWatch.Stop()
        _greenWatch.Stop()
        _yellowWatch.Stop()
        _orangeWatch.Stop()
        _redWatch.Stop()
    End Sub
End Class