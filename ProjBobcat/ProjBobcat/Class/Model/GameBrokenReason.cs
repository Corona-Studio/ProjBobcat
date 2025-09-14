using System;

namespace ProjBobcat.Class.Model;

[Flags]
public enum GameBrokenReason
{
    ParentVersionNotFound = 0,
    GameJsonCorrupted = 1 << 0,
    LackGameJson = 1 << 1,
    GamePathNotFound = 1 << 2,
    NoCandidateJsonFound = 1 << 3,
    CycleDepDetected = 1 << 4,

    // A special flag to indicate that the  parent game is broken.
    Parent = 1 << 16
}