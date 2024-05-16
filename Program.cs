using Microsoft.Win32;
using NAudio.Wave;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.ComponentModel.Design;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using UAssetAPI;
using UAssetAPI.ExportTypes;
using UAssetAPI.PropertyTypes.Objects;
using UAssetAPI.UnrealTypes;

namespace AutoMog
{
    internal class Program
    {

        // Copy Pasted DirSearch from stackoverflow and adapted to return all filepaths
        static List<String> DirSearch_ex3(string sDir, List<String> files)
        {
            try
            {
                //Console.WriteLine(sDir);

                foreach (string f in Directory.GetFiles(sDir))
                {
                    if (Path.GetExtension(f) == ".wav")
                    {
                        files.Add(f);
                    }
                }

                foreach (string d in Directory.GetDirectories(sDir))
                {
                    DirSearch_ex3(d, files);
                }
            }
            catch (System.Exception excpt)
            {
                Console.WriteLine(excpt.Message);
            }
            return files;
        }
        // Main menu, nothing too complex here
        int MogMenu()
        {
            int Choice = 0;
            Console.WriteLine("Menu:");
            Console.WriteLine("1- SQEXSEADMusic");
            Console.WriteLine("2- SQEXSEADSound");
            Console.Write("Choice: ");
            if (int.TryParse(Console.ReadLine(), out Choice))
                return Choice;
            else
                return 0;
        }
        // Uses UAssetAPI to generate a new .uasset/.uexp file with path and filename replaced
        UAsset CreateNewSQEXSEADUasset(string currentFile,string filepath, UAsset SQEXAudio){

            // Set the Uasset based on the user choices 
            string filename = Regex.Replace(Path.GetFileNameWithoutExtension(currentFile), @"\s+", "");
            string npath = filename + ".uasset";
            WaveFileReader currentWav = new WaveFileReader(currentFile);

            // Automatically change the uasset file's duration property based on the current .wav file duration
            NormalExport durationExport = (NormalExport)SQEXAudio.Exports[0];
            FloatPropertyData durationFloat = (FloatPropertyData)durationExport["Duration"];
            float.TryParse(currentWav.TotalTime.TotalSeconds.ToString(), out durationFloat.Value);


            // Change the parameters of the SQEX file depending on what type the user chose
            if (Path.GetFileNameWithoutExtension(SQEXAudio.FilePath) == "SQEXSEADMusic")
            {
                SQEXAudio.SetNameReference(0, FString.FromString(filepath + filename));
                SQEXAudio.SetNameReference(3, FString.FromString(filename));
            }
            else
            {
                SQEXAudio.SetNameReference(1, FString.FromString(filepath + filename));
                SQEXAudio.SetNameReference(8, FString.FromString(filename));
            }

            // Make new SQEX audio file and replace the reference to the new one
            SQEXAudio.Write(npath);
            SQEXAudio = new UAsset(npath, EngineVersion.VER_UE4_17);
            
            return SQEXAudio;
        }

        // Uses AudioMog to replace the audio file on the newly generated file       
        void AudioMogProc(string currentFile, string ProjectPath, UAsset SQEXAudio)
        {
            string filename = Regex.Replace(Path.GetFileNameWithoutExtension(currentFile), @"\s+", "");

            // AudioMog Process settings

            string audiomogpath = Directory.GetCurrentDirectory() + "/Templates/AudioMog.exe";
            ProcessStartInfo mogProc = new ProcessStartInfo(audiomogpath, SQEXAudio.FilePath.Replace("uasset", "uexp"));
            mogProc.UseShellExecute = false;
            mogProc.RedirectStandardOutput = true;
            
            // AudioMog the newly created uasset/uexp file
            Process proce = Process.Start(mogProc);
            Thread.Sleep(500);

            // Wait for Directory to be created
            while (!Directory.Exists(ProjectPath))
            {
                Thread.Sleep(500);
                Console.WriteLine(".");
            }

            proce.CloseMainWindow();
            proce.Close();

            // AudioMog rebuild uasset/uexp with given .wav

            File.Copy(currentFile, ProjectPath + filename + "_000.wav", true);

            Thread.Sleep(500);

            ProcessStartInfo rebuild = new ProcessStartInfo(audiomogpath, ProjectPath + "RebuildSettings.json");
            rebuild.UseShellExecute = false;
            rebuild.RedirectStandardOutput = true;

            Process rebuilde = Process.Start(rebuild);
            Thread.Sleep(500);

            // Wait for File to be created
            while (!File.Exists(ProjectPath + filename + ".uasset"))
            {
                Thread.Sleep(500);
                Console.Write(".");
            }
            
            rebuilde.CloseMainWindow();
            rebuilde.Close();
        }
        
        // Cleans any leftover temp files
        void CleanMog(string filepath, string ProjectPath, string filename, string currentDir)
        {
            //Move the new cooked assets into the output folder
            if (!Directory.Exists("Output"))
                Directory.CreateDirectory("Output");
            string outputDir = Regex.Replace(filepath, "^/game", "/Content", RegexOptions.IgnoreCase);

            Directory.CreateDirectory("Output" + outputDir);

            File.Copy(ProjectPath + filename + ".uasset", currentDir + "/Output" + outputDir + filename + ".uasset", true);
            File.Copy(ProjectPath + filename + ".uexp", currentDir + "/Output" + outputDir + filename + ".uexp", true);

            File.Delete(currentDir + "/" + filename + ".uasset");
            File.Delete(currentDir + "/" + filename + ".uexp");
            Directory.Delete(ProjectPath, true);
        }
        
        // Creates, ready to use in engine, dummy references of each file generated
        void DummyMog(bool isMusic, string currentDir, string filepath, string filename, UAsset DummyAudio)
        {
            // Set Dummies Templates
            if (isMusic)
            {
                DummyAudio = new UAsset(currentDir + "/Templates/MusicDummy.uasset", EngineVersion.VER_UE4_17);
                DummyAudio.SetNameReference(0, FString.FromString(filepath + filename));
                DummyAudio.SetNameReference(5, FString.FromString(filename));
            }
            else
            {
                DummyAudio = new UAsset(currentDir + "/Templates/SoundDummy.uasset", EngineVersion.VER_UE4_17);
                DummyAudio.SetNameReference(0, FString.FromString(filepath + filename));
                DummyAudio.SetNameReference(7, FString.FromString(filename));
            }


            if (!Directory.Exists("Dummy"))
                Directory.CreateDirectory("Dummy");


            //Dummy Audio
            string dummyDir = "Dummy" + Regex.Replace(filepath, "^/game", "/Content", RegexOptions.IgnoreCase);
            Directory.CreateDirectory(dummyDir);
            DummyAudio.Write(dummyDir + filename + ".uasset");
        }

        static void Main(string[] args)
        {
            // Get .wav if not .wav close (in batch case skip over non .wav files)
            // Menu Select (SQEXSEADMusic, SQEXSEADSound)

            // void CreateNewSQEXSEADUasset()
            // Edit Uasset Path/Filename
            // Create new Uasset

            // void AudioMogProc()
            // Audiomog its new .uexp and move project folder to Temp folder
            // Replace the .wav in the project folder with new one and then audiomog it

            // void CleanMog()
            // Move newly made Uasset/Uexp to Output folders
            // Delete Project folder

            
            // In case of Batch go to step 3

            // Check for any file provided and if so proceed with initializing variables
            if (args.Length == 0){
                Console.WriteLine("No file or folder provided. Exiting.");
                Environment.Exit(1);
            }
            Program program = new Program();
            UAsset SQEXTemplateAudio = new UAsset();
            UAsset SQEXAudio = new UAsset();
            UAsset DummyAudio = new UAsset();
            string currentDir = Directory.GetCurrentDirectory();
            string currentFile = "";
            string filepath = "";
            List<String> batchFiles = new List<String>();
            FileAttributes attr = File.GetAttributes(args[0]);
            bool batchFileMode = false;
            //Detect whether its a directory or file
            if ((attr & FileAttributes.Directory) == FileAttributes.Directory)
                batchFileMode = true;
            else{
                currentFile = args[0];
                if (Path.GetExtension(currentFile) != ".wav"){
                    Environment.Exit(1);
                }
            }

            bool isOnMenu = true;
            bool isMusic = false;
            //Menu
            while (isOnMenu)
            {
                switch (program.MogMenu())
                {
                    case 1:
                        SQEXTemplateAudio = new UAsset(currentDir + "/Templates/SQEXSEADMusic.uasset", EngineVersion.VER_UE4_17);
                        isOnMenu = false;
                        isMusic = true;
                        break;
                    case 2:
                        SQEXTemplateAudio = new UAsset(currentDir + "/Templates/SQEXSEADSound.uasset", EngineVersion.VER_UE4_17);
                        isOnMenu = false;
                        isMusic = false;
                        break;
                    default: Console.WriteLine("Choice isn't in the menu"); Thread.Sleep(500); break;

                }
                Console.Clear();
            }

            // IF args include a singular file
            if (!batchFileMode)
            {
                Console.Write("Set the path for the Audio File in Engine: ");
                filepath = Console.ReadLine();
                filepath = filepath.Replace("\n", "");
                if (!filepath.EndsWith("/"))
                    filepath += "/";

                SQEXAudio = program.CreateNewSQEXSEADUasset(currentFile, filepath, SQEXTemplateAudio);

                string ProjectPath = Regex.Replace(Path.GetFileNameWithoutExtension(currentFile), @"\s+", "") + "_Project/";

                program.AudioMogProc(currentFile, ProjectPath, SQEXAudio);

                program.CleanMog(filepath, ProjectPath, Path.GetFileNameWithoutExtension(currentFile), currentDir);

                program.DummyMog(isMusic, currentDir, filepath, Path.GetFileNameWithoutExtension(currentFile), DummyAudio);

            }
            else
            {
                // IF args includes a Folder
                batchFiles = DirSearch_ex3(args[0], batchFiles);

                foreach (string file in batchFiles)
                {
                    currentFile = file;

                    // IF user's root folder for batch thing is Content then remove content from path and also remove the filename, that's a mouthful yikes
                    filepath = "/Game/" + Regex.Replace(Regex.Replace(Path.GetDirectoryName(currentFile.TrimStart(Directory.GetCurrentDirectory().ToCharArray())), "^/content", "", RegexOptions.IgnoreCase), @"\\", "/") + "/";

                    Console.WriteLine("File " + Path.GetFileNameWithoutExtension(currentFile) + " index " + (batchFiles.FindIndex(a => a.Contains(file))+1) + " of " + batchFiles.Count);

                    // The whole automated process file by file
                    SQEXAudio = program.CreateNewSQEXSEADUasset(currentFile, filepath, SQEXTemplateAudio);

                    string ProjectPath = Regex.Replace(Path.GetFileNameWithoutExtension(currentFile), @"\s+", "") + "_Project/";

                    program.AudioMogProc(currentFile, ProjectPath, SQEXAudio);

                    program.CleanMog(filepath, ProjectPath, Regex.Replace(Path.GetFileNameWithoutExtension(currentFile), @"\s+", ""), currentDir);

                    program.DummyMog(isMusic, currentDir, filepath, Regex.Replace(Path.GetFileNameWithoutExtension(currentFile), @"\s+", ""), DummyAudio);

                    Console.Clear();
                }
            }

            //Exit Program
            Console.Clear();
            Console.Beep();
            Console.WriteLine("Process complete! \nYou may close the program now.");
            Environment.Exit(0);
        }
    }
}
