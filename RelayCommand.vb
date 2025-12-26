Imports System.Windows.Input

''' <summary>
''' Generic relay command for MVVM pattern
''' Allows commands to be bound in XAML and executed from ViewModel
''' </summary>
Public Class RelayCommand(Of T)
    Implements ICommand

    Private ReadOnly _execute As Action(Of T)
    Private ReadOnly _canExecute As Func(Of T, Boolean)

    Public Event CanExecuteChanged As EventHandler Implements ICommand.CanExecuteChanged

    Public Sub New(execute As Action(Of T), Optional canExecute As Func(Of T, Boolean) = Nothing)
        _execute = execute
        _canExecute = canExecute
    End Sub

    Public Function CanExecute(parameter As Object) As Boolean Implements ICommand.CanExecute
        If _canExecute Is Nothing Then
            Return True
        End If
        Return _canExecute(CType(parameter, T))
    End Function

    Public Sub Execute(parameter As Object) Implements ICommand.Execute
        _execute(CType(parameter, T))
    End Sub

    Public Sub RaiseCanExecuteChanged()
        RaiseEvent CanExecuteChanged(Me, EventArgs.Empty)
    End Sub
End Class

''' <summary>
''' Non-generic relay command for simple execute-only operations
''' </summary>
Public Class RelayCommand
    Implements ICommand

    Private ReadOnly _execute As Action
    Private ReadOnly _canExecute As Func(Of Boolean)

    Public Event CanExecuteChanged As EventHandler Implements ICommand.CanExecuteChanged

    Public Sub New(execute As Action, Optional canExecute As Func(Of Boolean) = Nothing)
        _execute = execute
        _canExecute = canExecute
    End Sub

    Public Function CanExecute(parameter As Object) As Boolean Implements ICommand.CanExecute
        If _canExecute Is Nothing Then
            Return True
        End If
        Return _canExecute()
    End Function

    Public Sub Execute(parameter As Object) Implements ICommand.Execute
        _execute()
    End Sub

    Public Sub RaiseCanExecuteChanged()
        RaiseEvent CanExecuteChanged(Me, EventArgs.Empty)
    End Sub
End Class