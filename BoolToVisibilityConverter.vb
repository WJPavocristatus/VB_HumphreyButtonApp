Imports System.Globalization
Imports System.Windows
Imports System.Windows.Data

''' <summary>
''' Converts boolean to Visibility
''' </summary>
Public Class BoolToVisibilityConverter
    Implements IValueConverter

    Public Function Convert(value As Object, targetType As Type, parameter As Object, culture As CultureInfo) As Object Implements IValueConverter.Convert
        If TypeOf value Is Boolean Then
            Dim boolValue = CType(value, Boolean)
            Return If(boolValue, Visibility.Visible, Visibility.Collapsed)
        End If
        Return Visibility.Collapsed
    End Function

    Public Function ConvertBack(value As Object, targetType As Type, parameter As Object, culture As CultureInfo) As Object Implements IValueConverter.ConvertBack
        If TypeOf value Is Visibility Then
            Dim visibility = CType(value, Visibility)
            Return visibility = Visibility.Visible
        End If
        Return False
    End Function
End Class