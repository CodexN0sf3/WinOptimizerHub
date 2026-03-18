# WinOptimizerHub 🛠️

An open-source, WPF-based Windows maintenance and optimization utility. 

This project was built from scratch in C# using a pure MVVM architecture to safely clean junk files, manage startup programs, optimize system performance, and disable unnecessary telemetry.

## 🚀 Features

* **Deep Cleanup:** Clears browser caches (Chromium/Gecko), WinSxS, DirectX, and font caches. Includes a Duplicate File Finder with quick/full hashing.
* **System Optimization:** Monitors RAM, applies SSD longevity tweaks, and disables OS-level tracking/telemetry.
* **Management Tools:** Edits Startup items directly from the Registry/folders, toggles Scheduled Tasks/Services, and includes a lightweight Uninstaller.
* **Safety First:** Automatically requests Administrator privileges and forces Windows Restore Point creation before aggressive operations.

## 💻 Tech Stack
* **Language:** C#
* **Framework:** .NET 4.8.1 (WPF)
* **Architecture:** Custom MVVM (No heavy external frameworks used)

## ⚙️ Getting Started
1. Clone the repository: `git clone https://github.com/CodexN0sf3/WinOptimizerHub.git`
2. Open `WinOptimizerHub.sln` in Visual Studio 2022.
3. Build the solution in `Release` or `Debug` mode.
4. Run the application (Note: The app will prompt for Administrator privileges).

## 🤝 Contributing & Code Review
I am currently a beginner in C# and built this project to deepen my understanding of WPF, multithreading, and Windows OS interactions. 
Code reviews, pull requests, and suggestions regarding architecture, exception handling, and performance optimizations are highly appreciated!
