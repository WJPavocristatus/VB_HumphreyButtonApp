Imports System.IO
Imports System.Text

Public Class DataController
    Private dataSb As New StringBuilder()
    Private trialSb As New StringBuilder()

    Private logWriter As StreamWriter = Nothing
    Private logLock As New Object()
    Private logFilePath As String = String.Empty

    Private hasSavedOnDisconnect As Boolean = False

    Public Sub New()
        Try
            Dim folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "PhidgetData", "logs")
            If Not Directory.Exists(folder) Then Directory.CreateDirectory(folder)
            logFilePath = Path.Combine(folder, $"phidget_log_{DateTime.UtcNow.ToString("yyyyMMdd_HHmms")}.log")
            logWriter = New StreamWriter(New FileStream(logFilePath, FileMode.Append, FileAccess.Write, FileShare.Read)) With {
                .AutoFlush = True
            }
            Log($"Log started: {DateTime.UtcNow:o}")
        Catch ex As Exception
            Console.WriteLine($"Failed to open log file: {ex.Message}")
        End Try
    End Sub

    Public Sub Log(message As String)
        Dim line = $"{DateTime.UtcNow:o} - {message}"
        SyncLock logLock
            Try
                If logWriter IsNot Nothing Then
                    logWriter.WriteLine(line)
                Else
                    Console.WriteLine(line)
                End If
            Catch ex As Exception
                Console.WriteLine($"Logging error: {ex.Message}")
            End Try
        End SyncLock
    End Sub

    Public Function RecordData(sessionStart As DateTime, subjectName As String, trainingMode As Boolean,
                               masterMs As Long, trialCount As Integer, btnCount As Integer,
                               activeMs As Long, stimAMs As Long, stimBMs As Long,
                               blueMs As Long, greenMs As Long, yellowMs As Long, orangeMs As Long, redMs As Long,
                               latencyMs As Long) As String
        Dim line As String
        If trainingMode Then
            line = $"Start Time: {sessionStart.ToFileTimeUtc}, {subjectName}, Training Mode?: {trainingMode}, Trial Timer: {masterMs / 1000} secs, Trial: {trialCount}, Button Presses: {btnCount}, Press duration: {activeMs / 1000} secs, Total StimA: {stimAMs / 1000} secs, Total StimB: {stimBMs / 1000} secs, Total Button Down time: {(stimAMs + stimBMs) / 1000} secs, Total Button Up time (Latency): {latencyMs / 1000} secs"
        Else
            line = $"Start Time: {sessionStart.ToFileTimeUtc}, {subjectName}, Training Mode?: {trainingMode}, Trial Timer: {masterMs / 1000} secs, Trial: {trialCount}, Button Presses: {btnCount}, Press duration: {activeMs / 1000} secs, Total StimA: {stimAMs / 1000} secs, Total StimB: {stimBMs / 1000} secs, Blue Time: {blueMs / 1000} secs, Green Time: {greenMs / 1000} secs, Yellow Time: {yellowMs / 1000} secs, Orange Time: {orangeMs / 1000} secs, Red Time: {redMs / 1000} secs, Total Button Down time: {(stimAMs + stimBMs) / 1000} secs, Total Button Up time (Latency): {latencyMs / 1000} secs"
        End If

        dataSb.AppendLine(line)
        Return line & Environment.NewLine
    End Function

    Public Function RecordTrial(sessionStart As DateTime, subjectName As String, trainingMode As Boolean,
                                trialCount As Integer, btnCount As Integer, masterMs As Long, targetMs As Integer,
                                aPressCt As Integer, stimAMs As Long, bPressCt As Integer, stimBMs As Long,
                                blueMs As Long, greenMs As Long, yellowMs As Long, orangeMs As Long, redMs As Long) As String
        Dim line As String
        If trainingMode Then
            line = $"Start Time: {sessionStart.ToFileTimeUtc}, {subjectName}, Training Mode?: {trainingMode}, Trial: {trialCount}, Button Presses: {btnCount}, Trial Duration: {masterMs / 1000} secs, Target Hold Time: {targetMs}, Stim A Presses: {aPressCt}, Total StimA: {stimAMs / 1000} secs, Stim B Presses: {bPressCt}, Total StimB: {stimBMs / 1000} secs, Time to first press (Master - [Up + Down]): {(masterMs - (stimAMs + stimBMs)) / 1000} secs"
        Else
            line = $"Start Time: {sessionStart.ToFileTimeUtc}, {subjectName}, Training Mode?: {trainingMode}, Trial: {trialCount}, Button Presses: {btnCount}, Trial Duration: {masterMs / 1000} secs, Target Hold Time: {targetMs}, Stim A Presses: {aPressCt}, Total StimA: {stimAMs / 1000} secs, Stim B Presses: {bPressCt}, Total StimB: {stimBMs / 1000} secs, Blue Time: {blueMs / 1000} secs, Green Time: {greenMs / 1000} secs, Yellow Time: {yellowMs / 1000} secs, Orange Time: {orangeMs / 1000} secs, Red Time: {redMs / 1000} secs, Time to first press (Master - [Up + Down]): {(masterMs - (stimAMs + stimBMs)) / 1000} secs"
        End If

        trialSb.AppendLine(line)
        Return line & Environment.NewLine
    End Function

    Public Sub SaveDataAuto(subjectName As String, stimAName As String, stimBName As String)
        Try
            Dim folder As String = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "PhidgetData", "autosave")
            If Not Directory.Exists(folder) Then Directory.CreateDirectory(folder)
            Dim outPath As String = Path.Combine(folder, $"{subjectName}_StimA-{stimAName}_StimB-{stimBName}_{Date.Now.ToFileTimeUtc}.csv")
            Using fs As New FileStream(outPath, FileMode.Create, FileAccess.Write, FileShare.None)
                Using sw As New StreamWriter(fs, Encoding.UTF8)
                    sw.Write(dataSb.ToString())
                End Using
            End Using
            Console.WriteLine($"Autosaved data to {outPath}")
        Catch ex As Exception
            Console.WriteLine($"Error autosaving data: {ex.Message}")
        End Try
    End Sub

    Public Sub SaveTrialDataAuto(subjectName As String)
        Try
            Dim folder As String = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "PhidgetData", "autosave")
            If Not Directory.Exists(folder) Then Directory.CreateDirectory(folder)
            Dim outPath As String = Path.Combine(folder, $"{subjectName}_Trials_{Date.Now.ToFileTimeUtc}.csv")
            Using fs As New FileStream(outPath, FileMode.Create, FileAccess.Write, FileShare.None)
                Using sw As New StreamWriter(fs, Encoding.UTF8)
                    sw.Write(trialSb.ToString())
                End Using
            End Using
            Console.WriteLine($"Autosaved trial data to {outPath}")
        Catch ex As Exception
            Console.WriteLine($"Error autosaving trial data: {ex.Message}")
        End Try
    End Sub

    Public Sub HandleDisconnectSave(manualSave As Boolean, manualTrialSave As Boolean, subjectName As String, stimAName As String, stimBName As String)
        If hasSavedOnDisconnect Or (manualSave AndAlso manualTrialSave) Then Return
        hasSavedOnDisconnect = True
        Try
            SaveDataAuto(subjectName, stimAName, stimBName)
            SaveTrialDataAuto(subjectName)
        Catch ex As Exception
            Console.WriteLine($"Error autosaving on disconnect: {ex.Message}")
        End Try
    End Sub

    Public Sub Close()
        Try
            If logWriter IsNot Nothing Then
                Log($"Log closed: {DateTime.UtcNow:o}")
                logWriter.Flush()
                logWriter.Close()
                logWriter.Dispose()
                logWriter = Nothing
            End If
        Catch
        End Try
    End Sub
End Class
