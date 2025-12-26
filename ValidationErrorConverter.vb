Imports System.Globalization
Imports System.Windows.Data

''' <summary>
''' Converter for displaying validation errors
''' </summary>
Public Class ValidationErrorConverter
    Implements IValueConverter

    Public Function Convert(value As Object, targetType As Type, parameter As Object, culture As CultureInfo) As Object Implements IValueConverter.Convert
        If value Is Nothing Then
            Return "No errors"
        End If

        Dim errors = CType(value, Dictionary(Of String, String))
        If errors.Count = 0 Then
            Return "No errors"
        End If

        Return String.Join(Environment.NewLine, errors.Values)
    End Function

    Public Function ConvertBack(value As Object, targetType As Type, parameter As Object, culture As CultureInfo) As Object Implements IValueConverter.ConvertBack
        Throw New NotImplementedException()
    End Function
End Class
