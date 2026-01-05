Imports System.Collections.Generic
Imports System.Linq

Public Class Trials
    Public Shared Trial0 As New TrialStimulusSequence With {
      .TrialID = 0,
      .Color1 = Brushes.Orange,
      .Color2 = Brushes.Green,
      .Color3 = Brushes.Yellow,
      .Color4 = Brushes.Blue,
      .Color5 = Brushes.Red
    }

    Public Shared Trial1 As New TrialStimulusSequence With {
        .TrialID = 1,
        .Color1 = Brushes.Green,
        .Color2 = Brushes.Blue,
        .Color3 = Brushes.Red,
        .Color4 = Brushes.Yellow,
        .Color5 = Brushes.Orange
    }

    Public Shared Trial2 As New TrialStimulusSequence With {
        .TrialID = 2,
        .Color1 = Brushes.Blue,
        .Color2 = Brushes.Yellow,
        .Color3 = Brushes.Orange,
        .Color4 = Brushes.Red,
        .Color5 = Brushes.Green
    }

    Public Shared Trial3 As New TrialStimulusSequence With {
        .TrialID = 3,
        .Color1 = Brushes.Red,
        .Color2 = Brushes.Blue,
        .Color3 = Brushes.Green,
        .Color4 = Brushes.Yellow,
        .Color5 = Brushes.Orange
    }

    Public Shared Trial4 As New TrialStimulusSequence With {
        .TrialID = 4,
        .Color1 = Brushes.Yellow,
        .Color2 = Brushes.Red,
        .Color3 = Brushes.Green,
        .Color4 = Brushes.Blue,
        .Color5 = Brushes.Orange
    }

    Public Shared Trial5 As New TrialStimulusSequence With {
        .TrialID = 5,
        .Color1 = Brushes.Yellow,
        .Color2 = Brushes.Blue,
        .Color3 = Brushes.Orange,
        .Color4 = Brushes.Red,
        .Color5 = Brushes.Green
    }

    Public Shared Trial6 As New TrialStimulusSequence With {
        .TrialID = 6,
        .Color1 = Brushes.Orange,
        .Color2 = Brushes.Blue,
        .Color3 = Brushes.Red,
        .Color4 = Brushes.Yellow,
        .Color5 = Brushes.Green
    }

    Public Shared Trial7 As New TrialStimulusSequence With {
        .TrialID = 7,
       .Color1 = Brushes.Blue,
       .Color2 = Brushes.Orange,
       .Color3 = Brushes.Green,
       .Color4 = Brushes.Yellow,
       .Color5 = Brushes.Red
   }

    Public Shared Trial8 As New TrialStimulusSequence With {
        .TrialID = 8,
        .Color1 = Brushes.Green,
        .Color2 = Brushes.Red,
        .Color3 = Brushes.Blue,
        .Color4 = Brushes.Orange,
        .Color5 = Brushes.Yellow
    }

    Public Shared Trial9 As New TrialStimulusSequence With {
        .TrialID = 9,
        .Color1 = Brushes.Red,
        .Color2 = Brushes.Yellow,
        .Color3 = Brushes.Orange,
        .Color4 = Brushes.Blue,
        .Color5 = Brushes.Green
    }

    Public Shared AllTrials As New List(Of TrialStimulusSequence) From {
        Trial0,
        Trial1,
        Trial2,
        Trial3,
        Trial4,
        Trial5,
        Trial6,
        Trial7,
        Trial8,
        Trial9
    }
End Class
