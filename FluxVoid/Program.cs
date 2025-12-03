using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Spectre.Console;

namespace SpectreFlux
{
    class Program
    {
        private static readonly Color PrimaryColor = Color.SpringGreen2;
        private static readonly Color AccentColor = Color.DeepSkyBlue1;
        private static readonly Color DangerColor = Color.Red1;
        private static readonly Color WarningColor = Color.Yellow;

        private static Queue<double> _cpuHistory = new Queue<double>();
        private static Queue<double> _gpuHistory = new Queue<double>();
        private static Queue<double> _ramHistory = new Queue<double>();
        private static readonly int MaxHistoryPoints = 40;

        private static bool _isRunning = true;

        static async Task Main(string[] args)
        {
            Console.OutputEncoding = System.Text.Encoding.UTF8;
            Console.CursorVisible = false;

            AnsiConsole.Write(
                new FigletText("SPECTRE_FLUX")
                    .LeftJustified()
                    .Color(PrimaryColor));

            AnsiConsole.MarkupLine("[bold grey]INITIALIZING CORE SYSTEMS...[/]");
            Thread.Sleep(1000);
            AnsiConsole.Clear();

            PerformanceCounter cpuCounter = null;
            PerformanceCounter ramCounter = null;

            if (System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Windows))
            {
                try
                {
                    cpuCounter = new PerformanceCounter("Processor", "% Processor Time", "_Total");
                    ramCounter = new PerformanceCounter("Memory", "% Committed Bytes In Use");
                    cpuCounter.NextValue();
                    ramCounter.NextValue();
                }
                catch
                {
                }
            }

            var layout = new Layout("Root")
                .SplitRows(
                    new Layout("Header").Size(3),
                    new Layout("Main").SplitColumns(
                        new Layout("LeftCol").SplitRows(
                            new Layout("CpuPanel"),
                            new Layout("GpuPanel")
                        ),
                        new Layout("RightCol").SplitRows(
                            new Layout("RamDiskPanel"),
                            new Layout("NetProcessPanel")
                        )
                    ),
                    new Layout("Footer").Size(3)
                );

            await AnsiConsole.Live(layout)
                .AutoClear(false)
                .Overflow(VerticalOverflow.Ellipsis)
                .Cropping(VerticalOverflowCropping.Top)
                .StartAsync(async ctx =>
                {
                    while (_isRunning)
                    {
                        if (Console.KeyAvailable)
                        {
                            var key = Console.ReadKey(true);
                            if (key.Key == ConsoleKey.Q)
                            {
                                _isRunning = false;
                                break;
                            }
                        }

                        var data = await Task.Run(() => CollectSystemData(cpuCounter, ramCounter));

                        UpdateHistories(data);

                        layout["Header"].Update(
                            new Panel(new Markup($"[bold {PrimaryColor}]SYSTEM STATUS :: ONLINE[/] | [grey]UPTIME: {DateTime.Now - Process.GetCurrentProcess().StartTime:hh\\:mm\\:ss}[/]"))
                                .Border(BoxBorder.None));

                        layout["CpuPanel"].Update(CreateChartPanel("CPU CORE FLUX", _cpuHistory, data.CpuUsage, PrimaryColor));

                        layout["GpuPanel"].Update(CreateChartPanel("GPU NEURAL NET", _gpuHistory, data.GpuUsage, AccentColor));

                        var ramBar = new BarChart()
                            .Width(40)
                            .Label("[grey]VOLATILE MEMORY[/]")
                            .CenterLabel()
                            .AddItem("RAM USAGE", data.RamUsage, data.RamUsage > 80 ? DangerColor : WarningColor);

                        var diskTable = new Table().Border(TableBorder.None).Expand();
                        diskTable.AddColumn("Drive");
                        diskTable.AddColumn("Free");
                        diskTable.AddColumn("Bar");

                        foreach (var drive in data.Drives)
                        {
                            string color = drive.FreeSpaceGB < 10 ? "red" : "green";
                            string bar = new string('|', (int)(drive.PercentUsed / 10));
                            diskTable.AddRow(
                                $"[bold]{drive.Name}[/]",
                                $"[{color}]{drive.FreeSpaceGB} GB[/]",
                                $"[{color}]{bar}[/]"
                            );
                        }

                        layout["RamDiskPanel"].Update(
                            new Panel(new Rows(ramBar, new Rule("[grey]STORAGE MATRIX[/]").LeftJustified(), diskTable))
                                .Header("MEMORY & STORAGE")
                                .BorderColor(WarningColor)
                                .Border(BoxBorder.Rounded));

                        var processTable = new Table().Border(TableBorder.None).Expand().Title("[grey]HEAVY THREADS[/]");
                        processTable.AddColumn("Proc");
                        processTable.AddColumn("RAM");
                        foreach (var p in data.Processes)
                        {
                            processTable.AddRow($"[green]{p.Name}[/]", $"[grey]{p.Memory}MB[/]");
                        }

                        var netInfo = $"[bold]UP:[/] {data.NetUp} KB/s\n[bold]DN:[/] {data.NetDown} KB/s";

                        layout["NetProcessPanel"].Update(
                            new Panel(new Rows(new Markup(netInfo), new Rule(), processTable))
                                .Header("NETWORK & TASKS")
                                .BorderColor(PrimaryColor)
                                .Border(BoxBorder.Rounded));

                        layout["Footer"].Update(
                            new Panel(new Markup("[black on white] [[Q]] QUIT [/] [grey] System Monitoring Active...[/]"))
                                .Border(BoxBorder.None));

                        ctx.Refresh();
                        await Task.Delay(250);
                    }
                });

            AnsiConsole.MarkupLine("[red]SYSTEM SHUTDOWN INITIATED...[/]");
        }

        struct DriveData { public string Name; public long FreeSpaceGB; public double PercentUsed; }
        struct ProcData { public string Name; public long Memory; }
        struct SystemData
        {
            public float CpuUsage;
            public float RamUsage;
            public float GpuUsage;
            public List<DriveData> Drives;
            public List<ProcData> Processes;
            public int NetUp;
            public int NetDown;
        }

        static SystemData CollectSystemData(PerformanceCounter cpu, PerformanceCounter ram)
        {
            var r = new Random();
            var data = new SystemData();

            try
            {
                data.CpuUsage = cpu != null ? cpu.NextValue() : r.Next(10, 50);
                data.RamUsage = ram != null ? ram.NextValue() : r.Next(40, 70);
            }
            catch
            {
                data.CpuUsage = r.Next(10, 50);
                data.RamUsage = r.Next(40, 70);
            }

            data.GpuUsage = (data.CpuUsage * 0.8f) + r.Next(-5, 15);
            if (data.GpuUsage < 0) data.GpuUsage = 0;
            if (data.GpuUsage > 100) data.GpuUsage = 100;
            data.Drives = new List<DriveData>();
            try
            {
                foreach (var d in DriveInfo.GetDrives().Where(x => x.IsReady).Take(2))
                {
                    long total = d.TotalSize;
                    long free = d.TotalFreeSpace;
                    double usedPercent = 100 * (1.0 - ((double)free / total));
                    data.Drives.Add(new DriveData { Name = d.Name, FreeSpaceGB = free / 1024 / 1024 / 1024, PercentUsed = usedPercent });
                }
            }
            catch
            {
                data.Drives.Add(new DriveData { Name = "C:\\", FreeSpaceGB = 120, PercentUsed = 65 });
            }

            data.Processes = new List<ProcData>();
            try
            {
                data.Processes = Process.GetProcesses()
                    .OrderByDescending(p => p.WorkingSet64)
                    .Take(5)
                    .Select(p => new ProcData { Name = p.ProcessName, Memory = p.WorkingSet64 / 1024 / 1024 })
                    .ToList();
            }
            catch
            {
                data.Processes.Add(new ProcData { Name = "SYSTEM_IDLE", Memory = 1024 });
            }

            data.NetUp = r.Next(10, 500);
            data.NetDown = r.Next(100, 20000);

            return data;
        }

        static void UpdateHistories(SystemData data)
        {
            AddToQueue(_cpuHistory, data.CpuUsage);
            AddToQueue(_gpuHistory, data.GpuUsage);
            AddToQueue(_ramHistory, data.RamUsage);
        }

        static void AddToQueue(Queue<double> queue, double val)
        {
            queue.Enqueue(val);
            if (queue.Count > MaxHistoryPoints) queue.Dequeue();
        }

        static Panel CreateChartPanel(string title, Queue<double> history, float current, Color color)
        {
            string[] blocks = { " ", "▂", "▃", "▄", "▅", "▆", "▇", "█" };
            string chart = "";

            foreach (var val in history)
            {
                int idx = (int)((val / 100.0) * (blocks.Length - 1));
                idx = Math.Clamp(idx, 0, blocks.Length - 1);
                string colTag = val > 80 ? "red" : (val > 50 ? "yellow" : color.ToMarkup());
                chart += $"[{colTag}]{blocks[idx]}[/]";
            }

            if (history.Count < MaxHistoryPoints) chart = chart.PadRight(MaxHistoryPoints, ' ');

            return new Panel(
                new Rows(
                    new Markup(chart),
                    new Markup($"\n[bold]LOAD:[/] {current:F1}%")
                ))
                .Header($"[bold]{title}[/]")
                .Border(BoxBorder.Heavy)
                .BorderColor(color);
        }
    }
}