using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Playwright;
using Xunit;
using OtpNet;

public class PowerAppsLoginCreateStudent
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
        // Používame absolútnu cestu pre adresár, aby sme zabezpečili, že adresár existuje a je na očakávanom mieste
        string baseDir = AppDomain.CurrentDomain.BaseDirectory;
        Console.WriteLine($"BaseDirectory: {baseDir}");
        
        // Ideme o 4 úrovne vyššie: bin/Release/net5.0 -> project root
        string projectRoot = Path.GetFullPath(Path.Combine(baseDir, "..", "..", ".."));
        Console.WriteLine($"ProjectRoot: {projectRoot}");
        
        string reportDir = Path.Combine(projectRoot, "test_report");
        Console.WriteLine($"ReportDir: {reportDir}");
        
        if (!Directory.Exists(reportDir))
        {
            Console.WriteLine($"Vytváram adresár: {reportDir}");
            Directory.CreateDirectory(reportDir);
        }
        else
        {
            Console.WriteLine($"Adresár už existuje: {reportDir}");
        }
        
        return reportDir;
    }

    [Fact]
    public async Task Test_PowerAppsCreateStudent()
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
            await page.GotoAsync("https://org57b3585d.crm4.dynamics.com/main.aspx?appid=0315801e-2fcf-ef11-8ee9-000d3a6576c9&forceUCI=1&pagetype=entitylist&etn=tom_student1&viewid=6c874a3c-91ba-4991-b94d-795c116e34c5&viewType=1039&lid=1742990628299");

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

            await page.WaitForSelectorAsync("text='New'", new PageWaitForSelectorOptions { State = WaitForSelectorState.Visible });
            await page.ClickAsync("text='New'");

            await page.FillAsync("input[aria-label='Email']", "example@example.com");
            await page.FillAsync("input[aria-label='First Name']", "John");
            await page.FillAsync("input[aria-label='Last Name']", "Doe");
            await page.FillAsync("input[aria-label='Phone']", "1234567890");

            await page.WaitForSelectorAsync("text='Save & Close'", new PageWaitForSelectorOptions { State = WaitForSelectorState.Visible });
            await page.ClickAsync("text='Save & Close'");

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
