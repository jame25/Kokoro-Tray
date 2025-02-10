<p align="center"> <img width="256" height="256" src="https://github.com/user-attachments/assets/6850bb03-57e6-45fa-9702-547f4a176b2c"></p>

Kokoro Tray is a small system tray utility for Windows, that utilizes [Kokoro-TTS](https://github.com/hexgrad/kokoro). It will read aloud the contents of your clipboard. You can stop or pause the speech at any time via an assigned hotkey.

## Features:

* Reads clipboard contents aloud
* Enable / Disable clipboard monitoring
* Many voices to choose from
* Change voice model
* Control speech rate
* Presets support
* Hotkeys support
* Pronunciation dictionaries support

## Prerequisites:

[.Net 8.0 Desktop Runtime](https://dotnet.microsoft.com/en-us/download/dotnet/8.0) is required to be installed.

## Install:

- Download the latest version of Kokoro Tray from [releases](https://github.com/jame25/Kokoro-Tray/releases/).
- Grab the latest base voice model (kokoro.onnx) from [here](https://github.com/taylorchu/kokoro-onnx/releases/).
- Download the voice pack from [here](https://github.com/jame25/Kokoro-Tray/releases/).
- <b>Extract all of the above into the same directory</b>.

## Dictionary Rules: (/dict)

Keywords found in the **ignore.dict** file are skipped over. 

If a keyword in the **banned.dict** file is detected, the entire line is skipped.

**replace.dict** functions as a replacement for a keyword or phrase, i.e LHC=Large Hadron Collider

## Thanks:

Thanks to the Kokoro-TTS team for their inovation and ingenuity.
Special thanks to Lyrcaxis for [KokoroSharp](https://github.com/Lyrcaxis/KokoroSharp).

## Support:

If you find this project helpful and would like to support its development, you can buy me a coffee on Ko-Fi:

[![ko-fi](https://ko-fi.com/img/githubbutton_sm.svg)](https://ko-fi.com/jame25)

