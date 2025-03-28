﻿using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Playwright;
using Xunit;
using OtpNet;

public class PowerAppsLoginTests
{
    private string? GetTotpCode(string secretKey)
    {
        try
        {
            Console.WriteLine($"Pokúšam sa vygenerovať kód s kľúčom: {secretKey}");
            var bytes = Base32Encoding.ToBytes(secretKey);
            var totp = new Totp(bytes);
            var code = totp.ComputeTotp();
            Console.WriteLine($"Úspešne vygenerovaný kód: {code}");
            return code;
        }
        catch (Exception e)
        {
            Console.WriteLine($"Chyba pri generovaní kódu: {e.Message}");
            return null;
        }
    }

    private string CreateReportDir()
    {
        // Skúsime najprv získať cestu z premennej prostredia, ktorá je nastavená v pipeline
        string? envReportDir = Environment.GetEnvironmentVariable("REPORT_DIRECTORY");
        
        string reportDir;
        if (!string.IsNullOrEmpty(envReportDir))
        {
            // Použijeme cestu z premennej prostredia, ak existuje
            reportDir = envReportDir;
            Console.WriteLine($"Používam adresár z premennej prostredia: {reportDir}");
        }
        else
        {
            // V prípade lokálneho spustenia alebo ak proměnná nie je nastavená
            // použijeme relatívnu cestu od aktuálneho adresára
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            Console.WriteLine($"BaseDirectory: {baseDir}");
            string projectRoot = Path.GetFullPath(Path.Combine(baseDir, "..", "..", ".."));
            Console.WriteLine($"ProjectRoot: {projectRoot}");
            reportDir = Path.Combine(projectRoot, "test_report");
            Console.WriteLine($"ReportDir z BaseDirectory: {reportDir}");

            // Alternatívne skúsiť použiť aktuálny adresár
            string currentDir = Environment.CurrentDirectory;
            Console.WriteLine($"CurrentDirectory: {currentDir}");
            string altReportDir = Path.Combine(currentDir, "test_report");
            Console.WriteLine($"Alternatívny ReportDir: {altReportDir}");
            
            // Použijeme alternatívnu cestu, ak vyzerá rozumnejšie
            if (currentDir.Contains("testyCS") && !baseDir.Contains("testyCS"))
            {
                reportDir = altReportDir;
                Console.WriteLine($"Používam alternatívnu cestu k adresáru pre report: {reportDir}");
            }
        }
        
        // Vytvoríme adresár, ak neexistuje
        if (!Directory.Exists(reportDir))
        {
            Console.WriteLine($"Vytváram adresár: {reportDir}");
            Directory.CreateDirectory(reportDir);
        }
        else
        {
            Console.WriteLine($"Adresár už existuje: {reportDir}");
        }
        
        // Pre istotu vypíšeme obsah adresára
        try
        {
            string[] files = Directory.GetFiles(reportDir);
            Console.WriteLine($"Počet súborov v adresári: {files.Length}");
            foreach (string file in files)
            {
                Console.WriteLine($"Súbor: {file}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Chyba pri výpise obsahu adresára: {ex.Message}");
        }
        
        return reportDir;
    }

    [Fact]
    public async Task Test_PowerAppsLogin()
    {
        // Credentials
        var credentials = new
        {
            Email = "tomas.krisica@w1kvs.onmicrosoft.com",
            Password = "Tomas07052003",
            TotpKey = "rhrsckmdnpm6zlmr"
        };

        // Create report directory
        string reportDir = CreateReportDir();
        string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");

        using var playwright = await Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
        {
            Headless = true,
            SlowMo = 50
        });

        var context = await browser.NewContextAsync();
        await context.Tracing.StartAsync(new TracingStartOptions
        {
            Screenshots = true,
            Snapshots = true,
            Sources = true
        });

        var page = await context.NewPageAsync();

        try
        {
            Console.WriteLine("Otváram prihlasovaciu stránku...");
            await page.GotoAsync("https://org57b3585d.crm4.dynamics.com");

            // Čakanie na prihlasovací formulár
            await page.WaitForSelectorAsync("input[type=\"email\"]", new PageWaitForSelectorOptions { State = WaitForSelectorState.Visible, Timeout = 30000 });

            // Zadanie emailu
            var emailInput = page.Locator("input[type=\"email\"]");
            await emailInput.FillAsync(credentials.Email);
            await page.ClickAsync("input[type=\"submit\"]");

            // Čakanie na pole pre heslo
            await page.WaitForSelectorAsync("input[type=\"password\"]", new PageWaitForSelectorOptions { State = WaitForSelectorState.Visible, Timeout = 30000 });

            // Zadanie hesla
            var passwordInput = page.Locator("input[type=\"password\"]");
            await passwordInput.FillAsync(credentials.Password);
            await page.ClickAsync("input[type=\"submit\"]");

            // Čakanie na zadanie verifikačného kódu
            string? totpCode = GetTotpCode(credentials.TotpKey);
            if (string.IsNullOrEmpty(totpCode))
            {
                throw new Exception("Nepodarilo sa vygenerovať TOTP kód");
            }
            await page.FillAsync("input[aria-label=\"Code\"]", totpCode);
            await page.ClickAsync("input[type=\"submit\"]");

            // Čakanie na tlačidlo Yes a kliknutie
            await page.WaitForSelectorAsync("text=Yes", new PageWaitForSelectorOptions { State = WaitForSelectorState.Visible, Timeout = 10000 });
            await page.ClickAsync("text=Yes");

            Console.WriteLine("Prihlásenie úspešné!");

            // Stop tracing and save file for successful test
            string tracePath = Path.Combine(reportDir, $"trace_success_{timestamp}.zip");
            await context.Tracing.StopAsync(new TracingStopOptions { Path = tracePath });
            Console.WriteLine($"Trace súbor úspešne uložený: {tracePath}");
        }
        catch
        {
            // Stop tracing and save file for failed test
            string tracePath = Path.Combine(reportDir, $"trace_error_{timestamp}.zip");
            await context.Tracing.StopAsync(new TracingStopOptions { Path = tracePath });
            Console.WriteLine($"Trace súbor uložený: {tracePath}");

            // Re-throw to mark the test as failed
            throw;
        }
        finally
        {
            await browser.CloseAsync();
        }
    }
}
