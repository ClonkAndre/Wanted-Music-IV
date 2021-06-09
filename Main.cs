// - - VWantedMusic - -
// Created by ItsClonkAndre
// Version 1.3

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using GTA;
using Un4seen.Bass;

namespace VWantedMusic {
    public class Main : Script {

        #region Variables and Enums
        private bool tempBool;
        private bool isHandleCurrentlyFadingOut;
        private bool loop;
        private bool fadeOut;
        private bool fadeIn;

        private int initalVolume;
        private int musicHandle;
        private int rndSeed;
        private int fadingSpeed;
        private int startAt;

        private Random rnd;

        private string[] musicFiles;
        private readonly string DataDir = Game.InstallFolder + @"\scripts\VWantedMusic";

        private enum AudioPlayMode
        {
            Play,
            Pause,
            Stop,
            None
        }
        #endregion

        #region Methods
        private int CreateFile(string file, bool createWithZeroDecibels, bool dontDestroyOnStreamEnd = false, bool loopStream = false)
        {
            if (!string.IsNullOrWhiteSpace(file)) {
                if (createWithZeroDecibels) {
                    if (dontDestroyOnStreamEnd) {
                        int handle;
                        if (loopStream) {
                            handle = Bass.BASS_StreamCreateFile(file, 0, 0, BASSFlag.BASS_STREAM_PRESCAN | BASSFlag.BASS_MUSIC_LOOP);
                        }
                        else {
                            handle = Bass.BASS_StreamCreateFile(file, 0, 0, BASSFlag.BASS_STREAM_PRESCAN);
                        }
                        SetStreamVolume(handle, 0f);
                        return handle;
                    }
                    else {
                        int handle = Bass.BASS_StreamCreateFile(file, 0, 0, BASSFlag.BASS_STREAM_AUTOFREE);
                        SetStreamVolume(handle, 0f);
                        return handle;
                    }
                }
                else {
                    if (dontDestroyOnStreamEnd) {
                        return Bass.BASS_StreamCreateFile(file, 0, 0, BASSFlag.BASS_STREAM_PRESCAN);
                    }
                    else {
                        return Bass.BASS_StreamCreateFile(file, 0, 0, BASSFlag.BASS_STREAM_AUTOFREE);
                    }
                }
            }
            else {
                return 0;
            }
        }
        public bool SetStreamVolume(int stream, float volume)
        {
            if (stream != 0) {
                return Bass.BASS_ChannelSetAttribute(stream, BASSAttribute.BASS_ATTRIB_VOL, volume / 100.0F);
            }
            else {
                return false;
            }
        }
        private AudioPlayMode GetStreamPlayMode(int stream)
        {
            if (stream != 0) {
                switch (Bass.BASS_ChannelIsActive(stream))  {
                    case BASSActive.BASS_ACTIVE_PLAYING:
                        return AudioPlayMode.Play;
                    case BASSActive.BASS_ACTIVE_PAUSED:
                        return AudioPlayMode.Pause;
                    case BASSActive.BASS_ACTIVE_STOPPED:
                        return AudioPlayMode.Stop;
                    default:
                        return AudioPlayMode.None;
                }
            }
            else {
                return AudioPlayMode.None;
            }
        }
        private async void FadeStreamOut(int stream, AudioPlayMode after, int fadingSpeed = 1000)
        {
            if (!isHandleCurrentlyFadingOut) {
                isHandleCurrentlyFadingOut = true;

                float handleVolume = 0f;
                Bass.BASS_ChannelSlideAttribute(stream, BASSAttribute.BASS_ATTRIB_VOL, 0f, fadingSpeed);

                while (Bass.BASS_ChannelIsActive(stream) == BASSActive.BASS_ACTIVE_PLAYING) {
                    Bass.BASS_ChannelGetAttribute(stream, BASSAttribute.BASS_ATTRIB_VOL, ref handleVolume);

                    if (handleVolume <= 0f) {
                        switch (after) {
                            case AudioPlayMode.Stop:
                                Bass.BASS_ChannelStop(stream);
                                isHandleCurrentlyFadingOut = false;
                                musicHandle = 0;
                                break;
                            case AudioPlayMode.Pause:
                                Bass.BASS_ChannelPause(stream);
                                isHandleCurrentlyFadingOut = false;
                                musicHandle = 0;
                                break;
                        }
                        break;
                    }

                    await Task.Delay(5);
                }
            }
        }
        private void FadeStreamIn(int stream, float fadeToVolumeLevel, int fadingSpeed)
        {
            Bass.BASS_ChannelPlay(stream, false);
            Bass.BASS_ChannelSlideAttribute(stream, BASSAttribute.BASS_ATTRIB_VOL, fadeToVolumeLevel / 100.0f, fadingSpeed);
        }

        private void PlayRandomSoundtrack()
        {
            try {
                if (loop) {
                    musicHandle = CreateFile(musicFiles[rnd.Next(0, musicFiles.Length)], fadeIn, true, true);
                }
                else {
                    musicHandle = CreateFile(musicFiles[rnd.Next(0, musicFiles.Length)], fadeIn);
                }

                if (musicHandle != 0) {
                    if (fadeIn) {
                        FadeStreamIn(musicHandle, initalVolume, fadingSpeed);
                    }
                    else {
                        Bass.BASS_ChannelPlay(musicHandle, false);
                    }
                }
                else {
                    Game.Console.Print("DeathMusicIV could not play file. musicHandle was zero.");
                }
            }
            catch (Exception ex) {
                Game.Console.Print("DeathMusicIV error in Play method. Details: " + ex.ToString());
            }
        }
        private void StopSoundtrack(bool instant = false)
        {
            if (musicHandle != 0) {
                if (GetStreamPlayMode(musicHandle) == AudioPlayMode.Play) {
                    if (instant) {
                        Bass.BASS_ChannelStop(musicHandle);
                        musicHandle = 0;
                    }
                    else {
                        if (fadeOut) {
                            FadeStreamOut(musicHandle, AudioPlayMode.Stop, fadingSpeed);
                        }
                        else {
                            Bass.BASS_ChannelStop(musicHandle);
                            musicHandle = 0;
                        }
                    }
                }
                else {
                    Bass.BASS_ChannelStop(musicHandle);
                    musicHandle = 0;
                }
            }
        }
        #endregion

        public Main()
        {
            try {
                // Setup Bass.dll
                Bass.BASS_Init(-1, 44100, BASSInit.BASS_DEVICE_DEFAULT, IntPtr.Zero);

                // Get and set settings
                rndSeed = Settings.GetValueInteger("RndSeed", "General", DateTime.Now.Millisecond);
                loop = Settings.GetValueBool("Loop", "Music", false);
                fadeOut = Settings.GetValueBool("FadeOut", "Music", true);
                fadeIn = Settings.GetValueBool("FadeIn", "Music", true);
                fadingSpeed = Settings.GetValueInteger("FadingSpeed", "Music", 3000);
                startAt = Settings.GetValueInteger("StartAt", "Music", 3);
                if (startAt < 1 | startAt > 6) {
                    startAt = 3;
                }
                initalVolume = Settings.GetValueInteger("Volume", "Music", 20);

                // Set new random
                rnd = new Random(rndSeed);

                this.Interval = 100;
                this.Tick += VWantedMusic_Tick;
                this.ConsoleCommand += VWantedMusic_ConsoleCommand;
            }
            catch (Exception ex) {
                Game.Console.Print("VWantedMusic error: " + ex.ToString() + " - Please let the developer know about this problem.");
            }
        }

        private void VWantedMusic_ConsoleCommand(object sender, ConsoleEventArgs e)
        {
            switch (e.Command.ToLower()) {
                case "vwmusic:reloadsettings":
                    try {
                        Game.Console.Print("VWantedMusic: Reloading settings...");
                        loop = Settings.GetValueBool("Loop", "Music", false);
                        fadeOut = Settings.GetValueBool("FadeOut", "Music", true);
                        fadeIn = Settings.GetValueBool("FadeIn", "Music", true);
                        fadingSpeed = Settings.GetValueInteger("FadingSpeed", "Music", 3000);
                        startAt = Settings.GetValueInteger("StartAt", "Music", 3);
                        if (startAt < 1 | startAt > 6) {
                            startAt = 3;
                        }
                        initalVolume = Settings.GetValueInteger("Volume", "Music", 20);
                        Game.Console.Print("VWantedMusic: Ready.");
                    }
                    catch (Exception ex) {
                        Game.Console.Print("VWantedMusic error while reloading settings: " + ex.Message);
                    }
                    break;
            }
        }

        private void VWantedMusic_Tick(object sender, EventArgs e)
        {
            if (Directory.Exists(DataDir)) {
                musicFiles = Directory.EnumerateFiles(DataDir).Where(file => Path.GetExtension(file) == ".mp3" || Path.GetExtension(file) == ".wav").ToArray();
                if (musicFiles.Length != 0) {
                    if (Game.LocalPlayer.WantedLevel >= startAt) {
                        if (!isHandleCurrentlyFadingOut) {
                            if (!tempBool) {
                                PlayRandomSoundtrack();
                                tempBool = true;
                            }
                        }
                    }
                    else if (Game.LocalPlayer.WantedLevel == 0) {
                        if (tempBool) {
                            StopSoundtrack();
                            tempBool = false;
                        }

                        if (!isHandleCurrentlyFadingOut) {
                            if (GetStreamPlayMode(musicHandle) == AudioPlayMode.Play) {
                                if (fadeOut) {
                                    FadeStreamOut(musicHandle, AudioPlayMode.Stop, fadingSpeed);
                                }
                                else {
                                    Bass.BASS_ChannelStop(musicHandle);
                                    musicHandle = 0;
                                }
                            }
                        }
                    }
                }
            }

        }

    }
}
