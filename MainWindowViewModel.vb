Imports System.ComponentModel

''' <summary>
''' ViewModel for MainWindow - handles all UI state and data binding
''' Separates presentation logic from business logic
''' </summary>
Public Class MainWindowViewModel
    Implements INotifyPropertyChanged

    Private _cumulativeButtonHoldTime As String = "0 secs"
    Private _currentHoldTime As String = "0 secs"
    Private _stimAHoldTime As String = "0 secs"
    Private _stimBHoldTime As String = "0 secs"
    Private _cumulativeButtonUpTime As String = "0 secs"
    Private _sessionData As String = ""
    Private _trialData As String = ""
    Private _totalPressValue As Double = 0
    Private _activeStimValue As Double = 0
    Private _stimAValue As Double = 0

    Public Event PropertyChanged As PropertyChangedEventHandler Implements INotifyPropertyChanged.PropertyChanged

    ''' <summary>
    ''' Cumulative button hold time (StimA + StimB combined)
    ''' </summary>
    Public Property CumulativeButtonHoldTime As String
        Get
            Return _cumulativeButtonHoldTime
        End Get
        Set(value As String)
            If value <> _cumulativeButtonHoldTime Then
                _cumulativeButtonHoldTime = value
                OnPropertyChanged(NameOf(CumulativeButtonHoldTime))
            End If
        End Set
    End Property

    ''' <summary>
    ''' Current active stimulus hold time
    ''' </summary>
    Public Property CurrentHoldTime As String
        Get
            Return _currentHoldTime
        End Get
        Set(value As String)
            If value <> _currentHoldTime Then
                _currentHoldTime = value
                OnPropertyChanged(NameOf(CurrentHoldTime))
            End If
        End Set
    End Property

    ''' <summary>
    ''' Total Stim A hold time
    ''' </summary>
    Public Property StimAHoldTime As String
        Get
            Return _stimAHoldTime
        End Get
        Set(value As String)
            If value <> _stimAHoldTime Then
                _stimAHoldTime = value
                OnPropertyChanged(NameOf(StimAHoldTime))
            End If
        End Set
    End Property

    ''' <summary>
    ''' Total Stim B hold time
    ''' </summary>
    Public Property StimBHoldTime As String
        Get
            Return _stimBHoldTime
        End Get
        Set(value As String)
            If value <> _stimBHoldTime Then
                _stimBHoldTime = value
                OnPropertyChanged(NameOf(StimBHoldTime))
            End If
        End Set
    End Property

    ''' <summary>
    ''' Cumulative button up time (latency)
    ''' </summary>
    Public Property CumulativeButtonUpTime As String
        Get
            Return _cumulativeButtonUpTime
        End Get
        Set(value As String)
            If value <> _cumulativeButtonUpTime Then
                _cumulativeButtonUpTime = value
                OnPropertyChanged(NameOf(CumulativeButtonUpTime))
            End If
        End Set
    End Property

    ''' <summary>
    ''' Session data text (all recorded press data)
    ''' </summary>
    Public Property SessionData As String
        Get
            Return _sessionData
        End Get
        Set(value As String)
            If value <> _sessionData Then
                _sessionData = value
                OnPropertyChanged(NameOf(SessionData))
            End If
        End Set
    End Property

    ''' <summary>
    ''' Trial data text (trial summaries)
    ''' </summary>
    Public Property TrialData As String
        Get
            Return _trialData
        End Get
        Set(value As String)
            If value <> _trialData Then
                _trialData = value
                OnPropertyChanged(NameOf(TrialData))
            End If
        End Set
    End Property

    ''' <summary>
    ''' Total press progress bar value (0-100%)
    ''' </summary>
    Public Property TotalPressValue As Double
        Get
            Return _totalPressValue
        End Get
        Set(value As Double)
            If value <> _totalPressValue Then
                _totalPressValue = value
                OnPropertyChanged(NameOf(TotalPressValue))
            End If
        End Set
    End Property

    ''' <summary>
    ''' Active stimulus progress bar value (0-100%)
    ''' </summary>
    Public Property ActiveStimValue As Double
        Get
            Return _activeStimValue
        End Get
        Set(value As Double)
            If value <> _activeStimValue Then
                _activeStimValue = value
                OnPropertyChanged(NameOf(ActiveStimValue))
            End If
        End Set
    End Property

    ''' <summary>
    ''' Stim A progress bar value (0-100%)
    ''' </summary>
    Public Property StimAValue As Double
        Get
            Return _stimAValue
        End Get
        Set(value As Double)
            If value <> _stimAValue Then
                _stimAValue = value
                OnPropertyChanged(NameOf(StimAValue))
            End If
        End Set
    End Property

    ''' <summary>
    ''' Raises PropertyChanged event
    ''' </summary>
    Protected Sub OnPropertyChanged(propertyName As String)
        RaiseEvent PropertyChanged(Me, New PropertyChangedEventArgs(propertyName))
    End Sub
End Class
