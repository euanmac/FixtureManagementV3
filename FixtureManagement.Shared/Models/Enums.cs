using System.ComponentModel.DataAnnotations;

namespace FixtureManagement.Shared.Models;

public enum AgeGroup
{
    U6, U7, U8, U9, U10, U11, U12, U13, U14, U15, U16, U17, U18, O50, Adult, External
}

public enum League
{
    [Display(Name = "South Bucks Mini Soccer Conference")]
    SouthBucksMini,
    [Display(Name = "Bucks Girls Football League")]
    BucksGirls,
    [Display(Name = "Wycombe and South Bucks Minor League")]
    WycAndSouthBucksMinor,
    [Display(Name = "Oxford Mail Youth Football League")]
    OxMailYouth,
    [Display(Name = "Oxfordshire Over 50s League")]
    OxOver50,
    [Display(Name = "High Wycombe Sunday Football Combination")]
    HighWycSunComb,
    [Display(Name = "Thames Valley Counties Women's Football League")]
    TVCWFL,
    [Display(Name = "Oxfordshire Girls Football League")]
    OxGirls,
    [Display(Name = "Junior Premier League")]
    JPL,
    [Display(Name = "Uhlsport Hellenic League")]
    UHL,
    [Display(Name = "Aylesbury and District Football League")]
    ADL,
    [Display(Name = "Spartan South Midlands")]
    SSM,
    [Display(Name = "Allied Counties League")]
    AlliedCounties
}

public enum TeamSize
{
    [Display(Name = "5 v 5")]
    _5v5,
    [Display(Name = "7 v 7")]
    _7v7,
    [Display(Name = "9 v 9")]
    _9v9,
    [Display(Name = "11 v 11")]
    _11v11
}

public enum Gender
{
    Male, Female, Mixed
}

public enum Division
{
    One = 1, Two = 2, Three = 3, Four = 4, Five = 5, Six = 6, Seven = 7, Eight = 8, Nine = 9, Ten = 10,
    Red = 20, White = 21, Blue = 22, Black = 23, Green = 24, Yellow = 25,
    West = 70, South = 71, North = 72, East = 73,
    Championship = 80, Premier = 81, Other = 100
}

public enum ContactType
{
    Manager = 0b_0000_0000,  // 0
    Coach = 0b_0000_0001,  // 1
    Admin = 0b_0000_0010,  // 2
    Other = 0b_0000_0011,  // 3
}
