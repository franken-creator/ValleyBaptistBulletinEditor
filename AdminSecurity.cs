using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Net.Mail;
using System.Security.Cryptography;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace ValleyBaptistBulletinEditor;

public static class AdminSecurity
{
    sealed class Record
    {
        public string Name { get; set; } = "";
        public string Role { get; set; } = "Publisher";
        public bool LayoutPermission { get; set; } = false;
        public string Salt { get; set; } = "";
        public string Hash { get; set; } = "";
        public string Email { get; set; } = "";
        public string Phone { get; set; } = "";
        // Legacy fields are retained for file compatibility but never used for resets.
        public string RecoverySalt { get; set; } = "";
        public string RecoveryHash { get; set; } = "";
    }
    sealed class Store { public List<Record> Accounts { get; set; } = new(); }
    public sealed record Session(string Name, string Role);
    static Session? current;
    public static Session? Current => current;
    public static bool IsPublisher => current != null && (current.Role.Equals("Publisher", StringComparison.OrdinalIgnoreCase) || current.Role.Equals("Publisher/Admin", StringComparison.OrdinalIgnoreCase));
    public static bool IsAdministrator => current != null && current.Role.Equals("Administrator", StringComparison.OrdinalIgnoreCase);
    public static bool IsAdmin => IsPublisher || IsAdministrator;
    public static bool CanEditLayout => IsPublisher || (IsAdministrator && Read().Accounts.Any(a => a.Name.Equals(current!.Name, StringComparison.OrdinalIgnoreCase) && a.LayoutPermission));
    public static void SignOut() => current = null;
    static bool AdminRole(Record r) => string.Equals(r.Role, "Publisher", StringComparison.OrdinalIgnoreCase) || string.Equals(r.Role, "Publisher/Admin", StringComparison.OrdinalIgnoreCase);

    static readonly string PathName = Path.Combine(Environment.GetFolderPath(
        Environment.SpecialFolder.LocalApplicationData), "ValleyBaptistBulletinEditor", "administrators.json");
    static Store Read() => File.Exists(PathName)
        ? JsonSerializer.Deserialize<Store>(File.ReadAllText(PathName)) ?? new Store() : new Store();
    // One verified recovery email per account; compare case-insensitively and ignore spaces.
    static bool EmailUsedByAnother(string email, string accountName) =>
        Read().Accounts.Any(a => !a.Name.Equals(accountName, StringComparison.OrdinalIgnoreCase) &&
            !string.IsNullOrWhiteSpace(a.Email) &&
            a.Email.Trim().Equals(email.Trim(), StringComparison.OrdinalIgnoreCase));

    static void Write(Store store)
    {
        var emails = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var a in store.Accounts)
        {
            if (string.IsNullOrWhiteSpace(a.Email)) continue;
            if (!emails.Add(a.Email.Trim()))
                throw new InvalidOperationException("That email address is already assigned to another account.");
        }
        Directory.CreateDirectory(Path.GetDirectoryName(PathName)!);
        string temp = PathName + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(store, new JsonSerializerOptions { WriteIndented = true }));
        File.Move(temp, PathName, true);
    }
    static string NewSalt() => Convert.ToBase64String(RandomNumberGenerator.GetBytes(16));
    static string Hash(string value, string salt) => Convert.ToBase64String(
        Rfc2898DeriveBytes.Pbkdf2(value, Convert.FromBase64String(salt), 210000, HashAlgorithmName.SHA256, 32));
    static bool Matches(string value, string salt, string hash)
    {
        try { return CryptographicOperations.FixedTimeEquals(Convert.FromBase64String(Hash(value,salt)), Convert.FromBase64String(hash)); }
        catch { return false; }
    }

    // Passwords can be viewed once for at most 30 seconds in each dialog.
    // After the timer expires or the window loses focus, reveal is disabled for that dialog.
    sealed class SecretInput
    {
        public PasswordBox Masked { get; } = new() { Height = 31 };
        public TextBox Visible { get; } = new() { Height = 31, Visibility = Visibility.Collapsed };
        public CheckBox Reveal { get; } = new() { Content = "Show password (30 seconds maximum)", Margin = new Thickness(0,5,0,0) };
        readonly DispatcherTimer timer = new() { Interval = TimeSpan.FromSeconds(30) };
        bool syncing, expired, started;
        public string Value => Visible.Visibility == Visibility.Visible ? Visible.Text : Masked.Password;
        public SecretInput(Window dialog)
        {
            Masked.PasswordChanged += (_,_) => { if (!syncing && Visible.Visibility == Visibility.Visible) { syncing=true;Visible.Text=Masked.Password;syncing=false; } };
            Visible.TextChanged += (_,_) => { if (!syncing) { syncing=true;Masked.Password=Visible.Text;syncing=false; } };
            Reveal.Checked += (_,_) =>
            {
                if (expired) { Reveal.IsChecked=false;return; }
                syncing=true;Visible.Text=Masked.Password;syncing=false;
                Visible.Visibility=Visibility.Visible;Masked.Visibility=Visibility.Collapsed;
                if (!started) { started=true;timer.Start(); }
            };
            Reveal.Unchecked += (_,_) => Hide();
            timer.Tick += (_,_) => Expire();
            dialog.Deactivated += (_,_) => { if (Reveal.IsChecked==true) Expire(); };
            dialog.Closed += (_,_) => { timer.Stop(); Visible.Clear(); Masked.Clear(); };
        }
        void Hide()
        {
            syncing=true;Masked.Password=Visible.Text;syncing=false;
            Visible.Visibility=Visibility.Collapsed;Masked.Visibility=Visibility.Visible;
        }
        void Expire()
        {
            Hide(); expired=true;Reveal.IsChecked=false;Reveal.IsEnabled=false;
            Reveal.Content="Password hidden — reopen window to reveal again";
        }
        public void Clear() { Masked.Clear();Visible.Clear(); }
        public void AddTo(StackPanel panel)
        {
            panel.Children.Add(Masked);panel.Children.Add(Visible);panel.Children.Add(Reveal);
        }
    }
    static Window Dialog(Window owner, string title, int height=235) => new()
    {
        Title=title,Owner=owner,WindowStartupLocation=WindowStartupLocation.CenterOwner,
        Width=440,Height=height,ResizeMode=ResizeMode.NoResize,ShowInTaskbar=false
    };
    static Button Button(string title) => new() { Content=title, Width=86,Margin=new Thickness(5) };
    static void Buttons(Window dialog, StackPanel panel, Action accept)
    {
        var row=new StackPanel { Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Right,Margin=new Thickness(0,12,0,0) };
        var cancel=Button("Cancel");var ok=Button("OK");ok.IsDefault=true;
        cancel.Click+=(_,_)=>dialog.DialogResult=false;
        ok.Click+=(_,_)=>accept();row.Children.Add(cancel);row.Children.Add(ok);panel.Children.Add(row);
    }
    static string? Ask(Window owner,string title,string question,bool hidden=true)
    {
        var dialog=Dialog(owner,title,hidden?240:200);
        var panel=new StackPanel { Margin=new Thickness(18) };
        panel.Children.Add(new TextBlock { Text=question,TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,0,0,9) });
        string result="";
        if (hidden)
        {
            var secret=new SecretInput(dialog);secret.AddTo(panel);
            if (title == "Sign In" && question == "Password:")
            {
                var forgot = new Button
                {
                    Content = "Forgot Password?",
                    Margin = new Thickness(0, 9, 0, 0),
                    Padding = new Thickness(8, 5, 8, 5),
                    HorizontalAlignment = HorizontalAlignment.Left
                };
                forgot.Click += (_, _) =>
                {
                    dialog.DialogResult = false;
                    ResetPassword(owner);
                };
                panel.Children.Add(forgot);
            }
            Buttons(dialog,panel,()=>{result=secret.Value;dialog.DialogResult=true;});
        }
        else
        {
            var plain=new TextBox { Height=31 };panel.Children.Add(plain);
            Buttons(dialog,panel,()=>{result=plain.Text.Trim();dialog.DialogResult=true;});
        }
        dialog.Content=panel;
        return dialog.ShowDialog()==true?result:null;
    }
    static string? AskNewPassword(Window owner,string title)
    {
        var dialog=Dialog(owner,title,385);
        var panel=new StackPanel { Margin=new Thickness(18) };
        panel.Children.Add(new TextBlock { Text="Password (minimum 6 characters)" });
        var first=new SecretInput(dialog);first.AddTo(panel);
        panel.Children.Add(new TextBlock { Text="Confirm password",Margin=new Thickness(0,10,0,0) });
        var second=new SecretInput(dialog);second.AddTo(panel);
        var warning=new TextBlock { Text="Enter the same password twice.",Margin=new Thickness(0,8,0,0) };
        panel.Children.Add(warning);string result="";
        Buttons(dialog,panel,()=>
        {
            if (first.Value.Length<6) { warning.Text="Minimum 6 characters required.";return; }
            if (first.Value!=second.Value) { warning.Text="Passwords do not match.";second.Clear();return; }
            result=first.Value;dialog.DialogResult=true;
        });
        dialog.Content=panel;
        return dialog.ShowDialog()==true?result:null;
    }

    // LOCAL TESTING ONLY: the Gmail credential is encrypted to this Windows user
    // by Export-Clixml. Never distribute it with the desktop application.
    private const string SenderEmail = "valleybaptistbulletinadmin@gmail.com";

    static string LoadLocalGmailPassword()
    {
        string credentialPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "ValleyBaptistBulletinEditor", "gmail-credential.xml");
        if (!File.Exists(credentialPath))
            throw new InvalidOperationException(
                "Gmail credential not found. Save it with Export-Clixml under this Windows account first.");

        // Read the Windows-protected PowerShell credential without putting the
        // password in process arguments, the source code, or a plaintext file.
        const string script = @"
$ErrorActionPreference = 'Stop'
$c = Import-Clixml -LiteralPath $env:VBB_CREDENTIAL_FILE
if ($c.UserName -ne 'valleybaptistbulletinadmin@gmail.com') {
    throw 'The saved Gmail sender account does not match.'
}
$b = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($c.Password)
try { [Console]::Out.Write([Runtime.InteropServices.Marshal]::PtrToStringBSTR($b)) }
finally { [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($b) }
";
        var start = new ProcessStartInfo("powershell.exe")
        {
            Arguments = "-NoProfile -NonInteractive -EncodedCommand " +
                Convert.ToBase64String(Encoding.Unicode.GetBytes(script)),
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };
        start.Environment["VBB_CREDENTIAL_FILE"] = credentialPath;
        using var process = Process.Start(start)
            ?? throw new InvalidOperationException("Could not start Windows PowerShell.");
        string password = process.StandardOutput.ReadToEnd();
        string error = process.StandardError.ReadToEnd();
        if (!process.WaitForExit(15000))
        {
            process.Kill();
            throw new InvalidOperationException("Reading the Gmail credential timed out.");
        }
        if (process.ExitCode != 0 || string.IsNullOrWhiteSpace(password))
            throw new InvalidOperationException(
                "Could not unlock the saved Gmail credential under this Windows account. " + error);
        return password;
    }

    static void SendVerificationEmail(string email, string code)
    {
        string password = LoadLocalGmailPassword();
        using var smtp = new SmtpClient("smtp.gmail.com", 587)
        {
            EnableSsl = true,
            UseDefaultCredentials = false,
            Credentials = new NetworkCredential(SenderEmail, password),
            Timeout = 20000
        };
        using var message = new MailMessage(
            SenderEmail, email, "Valley Baptist Bulletin verification PIN",
            $"Your verification PIN is {code}. It expires in 10 minutes. Do not share this code.");
        smtp.Send(message);
    }

    static bool VerifyEmail(Window owner, string email)
    {
        // Local prototype: PIN is held only in memory and is never written to disk.
        // For multi-computer deployment, PIN generation and verification must move
        // to a trusted central service, with rate limits and server-side expiry.
        string code = RandomNumberGenerator.GetInt32(0, 1000000).ToString("D6");
        try
        {
            SendVerificationEmail(email, code);
        }
        catch (Exception ex)
        {
            MessageBox.Show(owner, "Could not send verification email: " + ex.Message,
                "Email Delivery Failed", MessageBoxButton.OK, MessageBoxImage.Error);
            return false;
        }
        var expires = DateTimeOffset.UtcNow.AddMinutes(10);
        for (int attempt = 1; attempt <= 5 && DateTimeOffset.UtcNow < expires; attempt++)
        {
            string? entered = Ask(owner, "Verify Email",
                $"Enter the 6-digit PIN sent to {email}. Attempt {attempt} of 5:", false);
            if (entered == null) return false;
            if (entered == code && DateTimeOffset.UtcNow < expires) return true;
            MessageBox.Show(owner, "Incorrect or expired verification PIN.");
        }
        return false;
    }

    static bool ValidEmail(string value)
    {
        try { var address = new MailAddress(value); return address.Address == value && value.Contains('.'); }
        catch { return false; }
    }
    // Store verified numbers in E.164 format for the SMS delivery service.
    static string PhoneDigits(string value)
    {
        string digits = new string(value.Where(char.IsDigit).ToArray());
        if (digits.Length == 11 && digits[0] == '1') digits = digits.Substring(1);
        return digits;
    }
    static bool ValidPhone(string value) => PhoneDigits(value).Length == 10;
    static string SmsPhone(string value) => "+1" + PhoneDigits(value);

    static string FormatPhone(string value)
    {
        string digits = PhoneDigits(value);
        if (digits.Length > 10) digits = digits.Substring(0, 10);
        if (digits.Length == 0) return "1- ";
        if (digits.Length <= 3) return "1- (" + digits;
        if (digits.Length <= 6) return "1- (" + digits.Substring(0,3) + ") " + digits.Substring(3);
        return "1- (" + digits.Substring(0,3) + ") " + digits.Substring(3,3) + "-" + digits.Substring(6);
    }
    static void AttachPhoneFormatting(TextBox field)
    {
        bool formatting = false;
        field.TextChanged += (_, _) =>
        {
            if (formatting) return;
            formatting = true;
            string formatted = FormatPhone(field.Text);
            if (field.Text != formatted) field.Text = formatted;
            field.CaretIndex = field.Text.Length;
            formatting = false;
        };
        field.Text = FormatPhone(field.Text);
    }

    static bool SetRecoveryContacts(Window owner, Record account)
    {
        var dialog = Dialog(owner, "Verify Email Address", 360);
        dialog.Width = 510;
        var panel = new StackPanel { Margin = new Thickness(18) };
        panel.Children.Add(new TextBlock {
            Text = "Enter your email address twice. A six-digit PIN must be sent to and verified at that address before saving.",
            TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 10) });

        panel.Children.Add(new TextBlock { Text = "Email address" });
        var email = new TextBox { Text = account.Email, Height = 29 };
        panel.Children.Add(email);
        panel.Children.Add(new TextBlock { Text = "Confirm email address", Margin = new Thickness(0, 10, 0, 2) });
        var confirmEmail = new TextBox { Text = account.Email, Height = 29 };
        panel.Children.Add(confirmEmail);

        bool verified = false;
        string verifiedEmail = "";
        email.TextChanged += (_, _) => verified = false;
        confirmEmail.TextChanged += (_, _) => verified = false;

        var send = new Button { Content = "Send Email PIN / Verify", Margin = new Thickness(0, 12, 0, 0), Padding = new Thickness(8) };
        send.Click += (_, _) =>
        {
            string value = email.Text.Trim();
            if (!ValidEmail(value) || !string.Equals(value, confirmEmail.Text.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                MessageBox.Show(dialog, "Enter the same valid email address twice.");
                return;
            }
            if (EmailUsedByAnother(value, account.Name))
            {
                MessageBox.Show(dialog, "That email address is already registered to another account.");
                return;
            }
            verified = VerifyEmail(dialog, value);
            if (verified) { verifiedEmail = value; MessageBox.Show(dialog, "Email verified."); }
        };
        panel.Children.Add(send);

        var save = new Button { Content = "Save Verified Email", Margin = new Thickness(0, 12, 0, 0), Padding = new Thickness(8) };
        save.Click += (_, _) =>
        {
            if (!verified || !string.Equals(verifiedEmail, email.Text.Trim(), StringComparison.OrdinalIgnoreCase)
                || !string.Equals(verifiedEmail, confirmEmail.Text.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                MessageBox.Show(dialog, "Verify the email PIN before saving.");
                return;
            }
            if (EmailUsedByAnother(verifiedEmail, account.Name))
            {
                MessageBox.Show(dialog, "That email address is already registered to another account.");
                return;
            }
            account.Email = verifiedEmail;
            account.Phone = ""; // Email-only accounts no longer require a phone.
            dialog.DialogResult = true;
        };
        panel.Children.Add(save);
        dialog.Content = panel;
        return dialog.ShowDialog() == true;
    }

    static void AddAccount(Store store,Window owner)
    {
        string? name=Ask(owner,"Administrator Name","Enter a unique administrator name:",false);
        if(string.IsNullOrWhiteSpace(name))return;
        if(store.Accounts.Any(a=>string.Equals(a.Name,name,StringComparison.OrdinalIgnoreCase)))
        { MessageBox.Show(owner,"This administrator already exists.");return; }
        string? password=AskNewPassword(owner,"Create Administrator Password");
        if(password==null)return;
        var account=new Record { Name=name.Trim(),Salt=NewSalt() };
        if(store.Accounts.Count>0)
        {
            var role=Ask(owner,"Account Role","Enter ADMINISTRATOR or PUBLISHER:",false);
            if(role==null)return;
            if(role.Equals("ADMINISTRATOR",StringComparison.OrdinalIgnoreCase))account.Role="Administrator";
            else if(role.Equals("PUBLISHER",StringComparison.OrdinalIgnoreCase))account.Role="Publisher";
            else {MessageBox.Show(owner,"Enter ADMINISTRATOR or PUBLISHER.");return;}
        }
        if (!SetRecoveryContacts(owner, account))
        {
            MessageBox.Show(owner,"Account not created. Email verification is required.");
            return;
        }
        account.Hash=Hash(password,account.Salt);
        store.Accounts.Add(account);Write(store);
        MessageBox.Show(owner,"Account created. Email verified.");
    }
    public static bool Authenticate(Window owner)
    {
        if(current!=null)return CanEditLayout;
        try
        {
            var store=Read();
            if(store.Accounts.Count==0)
            {
                if(File.Exists(PathName)) {MessageBox.Show(owner,"No valid accounts found. Access locked.");return false;}
                if(MessageBox.Show(owner,"Create the first Publisher/Admin account?", "Initial Setup",MessageBoxButton.YesNo)!=MessageBoxResult.Yes)return false;
                AddAccount(store,owner);
                return false;
            }
            var name=Ask(owner,"Sign In","Account name:",false);
            if(name==null)return false;
            var account=store.Accounts.FirstOrDefault(a=>string.Equals(a.Name,name,StringComparison.OrdinalIgnoreCase));
            var password=Ask(owner,"Sign In","Password:");
            if(password==null)return false;
            if(account==null || !Matches(password,account.Salt,account.Hash))
            {MessageBox.Show(owner,"Invalid account name or password.");return false;}
            if (!AdminRole(account) && !account.Role.Equals("Administrator", StringComparison.OrdinalIgnoreCase))
            {
                MessageBox.Show(owner, "Regular users do not need to sign in. Close this dialog and edit the bulletin directly.");
                return false;
            }
            current=new Session(account.Name,account.Role);
            return CanEditLayout;
        }
        catch(Exception ex) {MessageBox.Show(owner,"Unable to sign in: "+ex.Message);return false;}
    }
    public static bool SignIn(Window owner)
    {
        Authenticate(owner);
        return current!=null;
    }
    public static void Manage(Window owner)
    {
        if(current==null) {MessageBox.Show(owner,"Sign in first.");return;}
        ShowAccountSettings(owner);
    }
    public static void ShowAccountSettings(Window owner)
    {
        if(current==null)return;
        var store=Read();
        var account=store.Accounts.FirstOrDefault(a=>string.Equals(a.Name,current.Name,StringComparison.OrdinalIgnoreCase));
        if(account==null)return;
        var dialog=Dialog(owner,"My Account — "+current.Name,400);
        var panel=new StackPanel {Margin=new Thickness(18)};
        panel.Children.Add(new TextBlock {Text="Signed in as: "+current.Name+" ("+(current.Role)+")",FontWeight=FontWeights.Bold,Margin=new Thickness(0,0,0,10)});
        panel.Children.Add(new TextBlock {Text="Recovery email: "+(string.IsNullOrEmpty(account.Email)?"Not configured":account.Email),TextWrapping=TextWrapping.Wrap});
        void Add(string label,Action action)
        {
            var b=new Button {Content=label,Margin=new Thickness(0,6,0,0),Padding=new Thickness(10)};
            b.Click+=(_,_)=>{dialog.Close();action();};panel.Children.Add(b);
        }
        Add("Register / Change My Verified Email",()=>UpdateRecovery(owner,current.Name));
        Add("Reset My Password (Email PIN)",()=>ResetPassword(owner));
        if(IsPublisher)
        {
            Add("Add Administrator / Publisher",()=>AddAccount(Read(),owner));
            Add("View / Manage All Accounts",()=>AllAccounts(owner));

        }
        dialog.Content=new ScrollViewer {Content=panel};dialog.ShowDialog();
    }
    static void UpdateRecovery(Window owner,string name)
    {
        var store=Read();var account=store.Accounts.FirstOrDefault(a=>string.Equals(a.Name,name,StringComparison.OrdinalIgnoreCase));
        if(account==null)return;
        if(SetRecoveryContacts(owner,account)) {Write(store);MessageBox.Show(owner,"Recovery email verified and saved.");}
    }
    static void AllAccounts(Window owner)
    {
        if(!IsPublisher)return;
        var store=Read();
        var names=string.Join("\n",store.Accounts.Select(a=>a.Name+" — "+a.Role + (a.LayoutPermission?" [Layout granted]":"")+" — Email: "+(string.IsNullOrEmpty(a.Email)?"Not set":a.Email)+" — Phone: "+(string.IsNullOrEmpty(a.Phone)?"Not set":a.Phone)));
        var dialog=Dialog(owner,"All User Accounts",480);dialog.Width=650;
        var panel=new StackPanel {Margin=new Thickness(16)};
        panel.Children.Add(new TextBox {Text=names,IsReadOnly=true,TextWrapping=TextWrapping.Wrap,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,Height=260});
        var edit=new Button {Content="Change Account Role",Margin=new Thickness(0,12,0,0)};
        edit.Click+=(_,_)=>
        {
            var name=Ask(dialog,"Change Role","Account name:",false);
            if(name==null)return;
            var record=store.Accounts.FirstOrDefault(a=>string.Equals(a.Name,name,StringComparison.OrdinalIgnoreCase));
            if(record==null){MessageBox.Show(dialog,"Account not found.");return;}
            var role=Ask(dialog,"Change Role","Enter ADMINISTRATOR or PUBLISHER:",false);
            if(role==null)return;
            var newRole=role.Trim().ToUpperInvariant();
            if(newRole!="ADMINISTRATOR" && newRole!="PUBLISHER")return;
            if(AdminRole(record) && newRole!="PUBLISHER" && store.Accounts.Count(AdminRole)<=1)
            {MessageBox.Show(dialog,"At least one Publisher must remain.");return;}
            record.Role=newRole=="PUBLISHER"?"Publisher":"Administrator";
            Write(store);MessageBox.Show(dialog,"Role updated. Changes take effect at next sign-in.");dialog.Close();
        };
        panel.Children.Add(edit);
        var grant=new Button {Content="Grant / Revoke Layout Permission",Margin=new Thickness(0,10,0,0)};
        grant.Click+=(_,_)=>
        {
            var name=Ask(dialog,"Layout Permission","Enter account name:",false);
            if(name==null)return;
            var record=store.Accounts.FirstOrDefault(a=>a.Name.Equals(name,StringComparison.OrdinalIgnoreCase));
            if(record==null){MessageBox.Show(dialog,"Account not found.");return;}
            if(AdminRole(record)){MessageBox.Show(dialog,"Publishers already have layout access.");return;}
            if(!record.Role.Equals("Administrator", StringComparison.OrdinalIgnoreCase)){MessageBox.Show(dialog,"Only administrators can be granted layout access.");return;}
            record.LayoutPermission=!record.LayoutPermission;
            Write(store);
            MessageBox.Show(dialog,record.LayoutPermission?"Layout access granted.":"Layout access revoked.");
            dialog.Close();
        };
        panel.Children.Add(grant);
        dialog.Content=panel;dialog.ShowDialog();
    }
    public static void ResetPassword(Window owner)
    {
        Store store;
        try { store=Read(); }catch { MessageBox.Show(owner,"Cannot read administrator accounts.");return; }
        var name=Ask(owner,"Reset Password","Administrator name:",false);
        if(name==null)return;
        var account=store.Accounts.FirstOrDefault(a=>string.Equals(a.Name,name,StringComparison.OrdinalIgnoreCase));
        if(account==null) { MessageBox.Show(owner,"Account not found or recovery unavailable.");return; }
        if (string.IsNullOrWhiteSpace(account.Email))
        {
            MessageBox.Show(owner, "No verified recovery email is registered.");
            return;
        }
        if (!VerifyEmail(owner, account.Email)) return;
        string? password=AskNewPassword(owner,"Reset Administrator Password");
        if(password==null)return;
        account.Salt=NewSalt();account.Hash=Hash(password,account.Salt);
        account.RecoveryHash="";account.RecoverySalt="";
        Write(store);
        MessageBox.Show(owner,"Password reset successfully. Sign in using your new password.");
    }
}
