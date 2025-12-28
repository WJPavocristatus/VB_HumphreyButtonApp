Public Class TrialStimulusSequence

    Public Property Color1 As SolidColorBrush
    Public Property Color2 As SolidColorBrush
    Public Property Color3 As SolidColorBrush
    Public Property Color4 As SolidColorBrush
    Public Property Color5 As SolidColorBrush

    Public Shared Trial0 As New TrialStimulusSequence With {
       .Color1 = Brushes.Orange,
       .Color2 = Brushes.Green,
       .Color4 = Brushes.Yellow,
       .Color3 = Brushes.Blue,
       .Color5 = Brushes.Red
   }

    Public Shared Trial1 As New TrialStimulusSequence With {
        .Color2 = Brushes.Green,
        .Color3 = Brushes.Blue,
        .Color1 = Brushes.Red,
        .Color4 = Brushes.Yellow,
        .Color5 = Brushes.Orange
    }

    Public Shared Trial2 As New TrialStimulusSequence With {
        .Color3 = Brushes.Blue,
        .Color4 = Brushes.Yellow,
        .Color5 = Brushes.Orange,
        .Color1 = Brushes.Red,
        .Color2 = Brushes.Green
    }

    Public Shared Trial3 As New TrialStimulusSequence With {
        .Color1 = Brushes.Red,
        .Color3 = Brushes.Blue,
        .Color2 = Brushes.Green,
        .Color4 = Brushes.Yellow,
        .Color5 = Brushes.Orange
    }

    Public Shared Trial4 As New TrialStimulusSequence With {
        .Color4 = Brushes.Yellow,
        .Color1 = Brushes.Red,
        .Color2 = Brushes.Green,
        .Color3 = Brushes.Blue,
        .Color5 = Brushes.Orange
    }

    Public Shared Trial5 As New TrialStimulusSequence With {
        .Color4 = Brushes.Yellow,
        .Color3 = Brushes.Blue,
        .Color5 = Brushes.Orange,
        .Color1 = Brushes.Red,
        .Color2 = Brushes.Green
    }

    Public Shared Trial6 As New TrialStimulusSequence With {
        .Color5 = Brushes.Orange,
        .Color3 = Brushes.Blue,
        .Color1 = Brushes.Red,
        .Color4 = Brushes.Yellow,
        .Color2 = Brushes.Green
    }

    Public Shared Trial7 As New TrialStimulusSequence With {
       .Color3 = Brushes.Blue,
       .Color5 = Brushes.Orange,
       .Color2 = Brushes.Green,
       .Color4 = Brushes.Yellow,
       .Color1 = Brushes.Red
   }

    Public Shared Trial8 As New TrialStimulusSequence With {
        .Color2 = Brushes.Green,
        .Color1 = Brushes.Red,
        .Color3 = Brushes.Blue,
        .Color5 = Brushes.Orange,
        .Color4 = Brushes.Yellow
    }

    Public Shared Trial9 As New TrialStimulusSequence With {
        .Color1 = Brushes.Red,
        .Color4 = Brushes.Yellow,
        .Color5 = Brushes.Orange,
        .Color3 = Brushes.Blue,
        .Color2 = Brushes.Green
    }
End Class
