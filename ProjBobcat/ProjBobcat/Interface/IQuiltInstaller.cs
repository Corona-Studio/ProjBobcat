﻿using System.Threading.Tasks;
using ProjBobcat.Class.Model.Quilt;

namespace ProjBobcat.Interface;

public interface IQuiltInstaller : IInstaller
{
    QuiltLoaderModel LoaderArtifact { get; set; }
    string MineCraftVersion { get; set; }
    string Install();
    Task<string> InstallTaskAsync();
}