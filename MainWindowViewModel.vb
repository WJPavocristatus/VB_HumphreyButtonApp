Imports System.ComponentModel
Imports System.Windows.Input

''' <summary>
''' ViewModel for MainWindow with data binding, validation, and commands
''' Handles all UI state and presentation logic
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
    Private _isSessionRunning As Boolean = False
    Private _canStartSession As Boolean = True
    Private _canSaveData As Boolean = False
    Private _validationErrors As New Dictionary(Of String, String)

    ' Commands
    Private _startSessionCommand As RelayCommand
    Private _saveDataCommand As RelayCommand
    Private _saveTrialDataCommand As RelayCommand

    ' Events
    Public Event PropertyChanged As PropertyChangedEventHandler Implements INotifyPropertyChanged.PropertyChanged
    Public Event SessionStartRequested As EventHandler
    Public Event SaveDataRequested As EventHandler
    Public Event SaveTrialDataRequested As EventHandler

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
                ValidateSessionData()
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
                ValidateTrialData()
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
    ''' Indicates if a session is currently running
    ''' </summary>
    Public Property IsSessionRunning As Boolean
        Get
            Return _isSessionRunning
        End Get
        Set(value As Boolean)
            If value <> _isSessionRunning Then
                _isSessionRunning = value
                OnPropertyChanged(NameOf(IsSessionRunning))
                UpdateCanExecuteStates()
            End If
        End Set
    End Property

    ''' <summary>
    ''' Indicates if session can be started
    ''' </summary>
    Public Property CanStartSession As Boolean
        Get
            Return _canStartSession
        End Get
        Set(value As Boolean)
            If value <> _canStartSession Then
                _canStartSession = value
                OnPropertyChanged(NameOf(CanStartSession))
            End If
        End Set
    End Property

    ''' <summary>
    ''' Indicates if data can be saved
    ''' </summary>
    Public Property CanSaveData As Boolean
        Get
            Return _canSaveData
        End Get
        Set(value As Boolean)
            If value <> _canSaveData Then
                _canSaveData = value
                OnPropertyChanged(NameOf(CanSaveData))
            End If
        End Set
    End Property

    ''' <summary>
    ''' Dictionary of validation errors
    ''' </summary>
    Public ReadOnly Property ValidationErrors As Dictionary(Of String, String)
        Get
            Return _validationErrors
        End Get
    End Property

    ''' <summary>
    ''' Indicates if there are any validation errors
    ''' </summary>
    Public ReadOnly Property HasValidationErrors As Boolean
        Get
            Return _validationErrors.Count > 0
        End Get
    End Property

    ''' <summary>
    ''' Command to start/stop session
    ''' </summary>
    Public ReadOnly Property StartSessionCommand As ICommand
        Get
            If _startSessionCommand Is Nothing Then
                _startSessionCommand = New RelayCommand(
                    Sub() RaiseEvent SessionStartRequested(Me, EventArgs.Empty),
                    Function() CanStartSession
                )
            End If
            Return _startSessionCommand
        End Get
    End Property

    ''' <summary>
    ''' Command to save session data
    ''' </summary>
    Public ReadOnly Property SaveDataCommand As ICommand
        Get
            If _saveDataCommand Is Nothing Then
                _saveDataCommand = New RelayCommand(
                    Sub() RaiseEvent SaveDataRequested(Me, EventArgs.Empty),
                    Function() CanSaveData
                )
            End If
            Return _saveDataCommand
        End Get
    End Property

    ''' <summary>
    ''' Command to save trial data
    ''' </summary>
    Public ReadOnly Property SaveTrialDataCommand As ICommand
        Get
            If _saveTrialDataCommand Is Nothing Then
                _saveTrialDataCommand = New RelayCommand(
                    Sub() RaiseEvent SaveTrialDataRequested(Me, EventArgs.Empty),
                    Function() CanSaveData
                )
            End If
            Return _saveTrialDataCommand
        End Get
    End Property

    ''' <summary>
    ''' Validates session data for save operations
    ''' </summary>
    Private Sub ValidateSessionData()
        _validationErrors.Remove("SessionData")

        If String.IsNullOrWhiteSpace(_sessionData) Then
            _validationErrors("SessionData") = "Session data is empty"
            CanSaveData = False
        Else
            CanSaveData = True
        End If

        OnPropertyChanged(NameOf(HasValidationErrors))
    End Sub

    ''' <summary>
    ''' Validates trial data for save operations
    ''' </summary>
    Private Sub ValidateTrialData()
        _validationErrors.Remove("TrialData")

        If String.IsNullOrWhiteSpace(_trialData) Then
            _validationErrors("TrialData") = "Trial data is empty"
            CanSaveData = False
        Else
            CanSaveData = True
        End If

        OnPropertyChanged(NameOf(HasValidationErrors))
    End Sub

    ''' <summary>
    ''' Updates command CanExecute states
    ''' </summary>
    Private Sub UpdateCanExecuteStates()
        If _startSessionCommand IsNot Nothing Then
            _startSessionCommand.RaiseCanExecuteChanged()
        End If
    End Sub

    ''' <summary>
    ''' Raises PropertyChanged event
    ''' </summary>
    Protected Sub OnPropertyChanged(propertyName As String)
        RaiseEvent PropertyChanged(Me, New PropertyChangedEventArgs(propertyName))
    End Sub

    ''' <summary>
    ''' Clears all validation errors
    ''' </summary>
    Public Sub ClearValidationErrors()
        _validationErrors.Clear()
        OnPropertyChanged(NameOf(HasValidationErrors))
    End Sub

    ''' <summary>
    ''' Adds validation error
    ''' </summary>
    Public Sub AddValidationError(key As String, message As String)
        _validationErrors(key) = message
        OnPropertyChanged(NameOf(HasValidationErrors))
    End Sub

    ''' <summary>
    ''' Checks if property has validation error
    ''' </summary>
    Public Function HasError(propertyName As String) As Boolean
        Return _validationErrors.ContainsKey(propertyName)
    End Function

    ''' <summary>
    ''' Gets validation error for property
    ''' </summary>
    Public Function GetError(propertyName As String) As String
        If _validationErrors.ContainsKey(propertyName) Then
            Return _validationErrors(propertyName)
        End If
        Return String.Empty
    End Function
End Class