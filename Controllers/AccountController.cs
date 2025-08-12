using System.Net.Mail;
using System.Security.Claims;
using System.Text;
using System.Text.Encodings.Web;
using GStore.Data;
using GStore.Helpers;
using GStore.Models;
using GStore.Services.EmailService;
using GStore.ViewModels;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;

namespace GStore.Controllers;
public class AccountController : Controller
{
    private readonly ILogger<AccountController> _logger;
    private readonly SignInManager<Usuario> _signInManager;
    private readonly UserManager<Usuario> _userManager;
    private readonly IWebHostEnvironment _host;
    private readonly AppDbContext _db;
    private readonly IEmailSender _emailSender;

    public AccountController(
        ILogger<AccountController> logger,
        SignInManager<Usuario> signInManager,
        UserManager<Usuario> userManager,
        IWebHostEnvironment host,
        IEmailSender emailSender,
        AppDbContext db
    )
    {
        _logger = logger;
        _signInManager = signInManager;
        _userManager = userManager;
        _host = host;
        _emailSender = emailSender;
        _db = db;
    }

    [HttpGet]
    public IActionResult Login(string returnUrl)
    {
        LoginVM login = new()
        {
            UrlRetorno = returnUrl ?? Url.Content("~/")
        };
        return View(login);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginVM login)
    {
        if (ModelState.IsValid)
        {
            string userName = login.Email;
            if (IsValidEmail(login.Email))
            {
                var user = await _userManager.FindByEmailAsync(login.Email);
                if (user != null)
                    userName = user.UserName;
            }

            var result = await _signInManager.PasswordSignInAsync(
                userName, login.Senha, login.Lembrar, lockoutOnFailure: true
            );

            if (result.Succeeded)
            {
                _logger.LogInformation($"Usuário {login.Email} acessou o sistema");
                return LocalRedirect(login.UrlRetorno);
            }

            if (result.IsLockedOut)
            {
                _logger.LogWarning($"Usuário {login.Email} está bloqueado");
                ModelState.AddModelError("", "Sua conta está bloqueada, aguarde alguns minutos e tente novamentel!!");
            }
            else
            if (result.IsNotAllowed)
            {
                _logger.LogWarning($"Usuário {login.Email} não confirmou sua conta");
                ModelState.AddModelError(string.Empty, "Sua conta não está confirmada, verifique seu email!!");
            }
            else
                ModelState.AddModelError(string.Empty, "Usuário e/ou Senha Inválidos!!!");
        }
        return View(login);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        _logger.LogInformation($"Usuário {ClaimTypes.Email} fez logoff");
        await _signInManager.SignOutAsync();
        return RedirectToAction("Index", "Home");
    }

    [HttpGet]
    public IActionResult Registro()
    {
        RegistroVM register = new();
        return View(register);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Registro(RegistroVM registro)
    {
        if (ModelState.IsValid)
        {
            var usuario = Activator.CreateInstance<Usuario>();
            usuario.Nome = registro.Nome;
            usuario.DataNascimento = registro.DataNascimento;
            usuario.UserName = registro.Email;
            usuario.NormalizedUserName = registro.Email.ToUpper();
            usuario.Email = registro.Email;
            usuario.NormalizedEmail = registro.Email.ToUpper();
            usuario.EmailConfirmed = true;
            var result = await _userManager.CreateAsync(usuario, registro.Senha);

            if (result.Succeeded)
            {
                _logger.LogInformation($"Novo usuário registrado com o email {registro.Email}.");

                await _userManager.AddToRoleAsync(usuario, "Cliente");

                if (registro.Foto != null)
                {
                    string nomeArquivo = usuario.Id + Path.GetExtension(registro.Foto.FileName);
                    string caminho = Path.Combine(_host.WebRootPath, @"img\usuarios");
                    string novoArquivo = Path.Combine(caminho, nomeArquivo);
                    using (var stream = new FileStream(novoArquivo, FileMode.Create))
                    {
                        registro.Foto.CopyTo(stream);
                    }
                    usuario.Foto = @"\img\usuarios\" + nomeArquivo;
                    await _db.SaveChangesAsync();
                }

                var code = await _userManager.GenerateEmailConfirmationTokenAsync(usuario);
                code = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(code));
                var url = $"localhost:5062/Account/ConfirmarEmail?id={usuario.Id}&code={code}";
                await _emailSender.SendEmailAsync(new([usuario.Email], "GStore - Criação de Conta",
                    GetConfirmEmailHtml(HtmlEncoder.Default.Encode(url)), null));

                TempData["Success"] = "Conta Criada com Sucesso!";
                return RedirectToAction(nameof(Login));
            }

            foreach (var error in result.Errors)
                ModelState.AddModelError(string.Empty, TranslateIdentityErrors.TranslateErrorMessage(error.Code));
        }
        return View(registro);
    }

    public IActionResult AccessDenied()
    {
        return View();
    }

    public bool IsValidEmail(string email)
    {
        try
        {
            MailAddress m = new(email);
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
    }

    #region Private Methods
    private string GetConfirmEmailHtml(string url)
    {
        var email = @"
            <!DOCTYPE html>
            <html>
            <head>

            <meta charset=""utf-8"">
            <meta http-equiv=""x-ua-compatible"" content=""ie=edge"">
            <title>Confirmação de Conta</title>
            <meta name=""viewport"" content=""width=device-width, initial-scale=1"">
            <style type=""text/css"">
            @media screen {
                @font-face {
                font-family: 'Source Sans Pro';
                font-style: normal;
                font-weight: 400;
                src: local('Source Sans Pro Regular'), local('SourceSansPro-Regular'), url(https://fonts.gstatic.com/s/sourcesanspro/v10/ODelI1aHBYDBqgeIAH2zlBM0YzuT7MdOe03otPbuUS0.woff) format('woff');
                }
                @font-face {
                font-family: 'Source Sans Pro';
                font-style: normal;
                font-weight: 700;
                src: local('Source Sans Pro Bold'), local('SourceSansPro-Bold'), url(https://fonts.gstatic.com/s/sourcesanspro/v10/toadOcfmlt9b38dHJxOBGFkQc6VGVFSmCnC_l7QZG60.woff) format('woff');
                }
            }
            body,
            table,
            td,
            a {
                -ms-text-size-adjust: 100%; /* 1 */
                -webkit-text-size-adjust: 100%; /* 2 */
            }
            table,
            td {
                mso-table-rspace: 0pt;
                mso-table-lspace: 0pt;
            }
            img {
                -ms-interpolation-mode: bicubic;
            }
            a[x-apple-data-detectors] {
                font-family: inherit !important;
                font-size: inherit !important;
                font-weight: inherit !important;
                line-height: inherit !important;
                color: inherit !important;
                text-decoration: none !important;
            }
            div[style*=""margin: 16px 0;""] {
                margin: 0 !important;
            }
            body {
                width: 100% !important;
                height: 100% !important;
                padding: 0 !important;
                margin: 0 !important;
            }
            table {
                border-collapse: collapse !important;
            }
            a {
                color: #1a82e2;
            }
            img {
                height: auto;
                line-height: 100%;
                text-decoration: none;
                border: 0;
                outline: none;
            }
            </style>

            </head>
            <body style=""background-color: #e9ecef;"">

            <!-- start preheader -->
            <div class=""preheader"" style=""display: none; max-width: 0; max-height: 0; overflow: hidden; font-size: 1px; line-height: 1px; color: #fff; opacity: 0;"">
                Plataforma Benchmarking CMAA - Confirmação de Conta.
            </div>
            <!-- end preheader -->

            <!-- start body -->
            <table border=""0"" cellpadding=""0"" cellspacing=""0"" width=""100%"">

                <!-- start logo -->
                <tr>
                <td align=""center"" bgcolor=""#e9ecef"">
                    <!--[if (gte mso 9)|(IE)]>
                    <table align=""center"" border=""0"" cellpadding=""0"" cellspacing=""0"" width=""600"">
                    <tr>
                    <td align=""center"" valign=""top"" width=""600"">
                    <![endif]-->
                    <table border=""0"" cellpadding=""0"" cellspacing=""0"" width=""100%"" style=""max-width: 600px;"">
                    <tr>
                        <td align=""center"" valign=""top"" style=""padding: 36px 24px;"">
                        <a href=""localhost:5062"" target=""_blank"" style=""display: inline-block;"">
                            GStore                            
                        </a>
                        </td>
                    </tr>
                    </table>
                    <!--[if (gte mso 9)|(IE)]>
                    </td>
                    </tr>
                    </table>
                    <![endif]-->
                </td>
                </tr>
                <!-- end logo -->

                <!-- start hero -->
                <tr>
                <td align=""center"" bgcolor=""#e9ecef"">
                    <!--[if (gte mso 9)|(IE)]>
                    <table align=""center"" border=""0"" cellpadding=""0"" cellspacing=""0"" width=""600"">
                    <tr>
                    <td align=""center"" valign=""top"" width=""600"">
                    <![endif]-->
                    <table border=""0"" cellpadding=""0"" cellspacing=""0"" width=""100%"" style=""max-width: 600px;"">
                    <tr>
                        <td align=""left"" bgcolor=""#ffffff"" style=""padding: 36px 24px 0; font-family: 'Source Sans Pro', Helvetica, Arial, sans-serif; border-top: 3px solid #d4dadf;"">
                            <h1 style=""margin: 0; font-size: 32px; font-weight: 700; letter-spacing: -1px; line-height: 48px;"">Confirmação de Endereço de Email</h1>
                        </td>
                    </tr>
                    </table>
                    <!--[if (gte mso 9)|(IE)]>
                    </td>
                    </tr>
                    </table>
                    <![endif]-->
                </td>
                </tr>
                <!-- end hero -->

                <!-- start copy block -->
                <tr>
                <td align=""center"" bgcolor=""#e9ecef"">
                    <!--[if (gte mso 9)|(IE)]>
                    <table align=""center"" border=""0"" cellpadding=""0"" cellspacing=""0"" width=""600"">
                    <tr>
                    <td align=""center"" valign=""top"" width=""600"">
                    <![endif]-->
                    <table border=""0"" cellpadding=""0"" cellspacing=""0"" width=""100%"" style=""max-width: 600px;"">

                    <!-- start copy -->
                    <tr>
                        <td align=""left"" bgcolor=""#ffffff"" style=""padding: 24px; font-family: 'Source Sans Pro', Helvetica, Arial, sans-serif; font-size: 16px; line-height: 24px;"">
                        <p style=""margin: 0;"">Clique no botão abaixo para confirmar a criação de sua conta. Se você não solicitou acesso ao GStore, apenas exclua esse email.</p>
                        </td>
                    </tr>
                    <!-- end copy -->

                    <!-- start button -->
                    <tr>
                        <td align=""left"" bgcolor=""#ffffff"">
                        <table border=""0"" cellpadding=""0"" cellspacing=""0"" width=""100%"">
                            <tr>
                            <td align=""center"" bgcolor=""#ffffff"" style=""padding: 12px;"">
                                <table border=""0"" cellpadding=""0"" cellspacing=""0"">
                                <tr>
                                    <td align=""center"" bgcolor=""darkgreen"" style=""border-radius: 6px;"">
                                    <a href=""" + url + @""" target=""_blank"" style=""display: inline-block; padding: 16px 36px; font-family: 'Source Sans Pro', Helvetica, Arial, sans-serif; font-size: 16px; color: #ffffff; text-decoration: none; border-radius: 6px;"">Confirmar Conta</a>
                                    </td>
                                </tr>
                                </table>
                            </td>
                            </tr>
                        </table>
                        </td>
                    </tr>
                    <!-- end button -->

                    <!-- start copy -->
                    <tr>
                        <td align=""left"" bgcolor=""#ffffff"" style=""padding: 24px; font-family: 'Source Sans Pro', Helvetica, Arial, sans-serif; font-size: 16px; line-height: 24px;"">
                            <p style=""margin: 0;"">Se isto não funcionar, clique <a href=""" + url + @""" target=""_blank"">AQUI</a> para continuar.</p>
                        </td>
                    </tr>
                    <!-- end copy -->

                    <!-- start copy -->
                    <tr>
                        <td align=""left"" bgcolor=""#ffffff"" style=""padding: 20px; font-family: 'Source Sans Pro', Helvetica, Arial, sans-serif; font-size: 16px; line-height: 24px; border-bottom: 3px solid #d4dadf"">
                        <p style=""margin: 0;"">Atenciosamente,<br> GStore</p>
                        </td>
                    </tr>
                    <!-- end copy -->

                    </table>
                    <!--[if (gte mso 9)|(IE)]>
                    </td>
                    </tr>
                    </table>
                    <![endif]-->
                </td>
                </tr>
                <!-- end copy block -->

                <!-- start footer -->
                <tr>
                <td align=""center"" bgcolor=""#e9ecef"" style=""padding: 24px;"">
                    <!--[if (gte mso 9)|(IE)]>
                    <table align=""center"" border=""0"" cellpadding=""0"" cellspacing=""0"" width=""600"">
                    <tr>
                    <td align=""center"" valign=""top"" width=""600"">
                    <![endif]-->
                    <table border=""0"" cellpadding=""0"" cellspacing=""0"" width=""100%"" style=""max-width: 600px;"">

                    <!-- start permission -->
                    <tr>
                        <td align=""center"" bgcolor=""#e9ecef"" style=""padding: 12px 24px; font-family: 'Source Sans Pro', Helvetica, Arial, sans-serif; font-size: 14px; line-height: 20px; color: #666;"">
                        <p style=""margin: 0;"">Você recebeu este email por solicitar acesso ao site GStore. Se você não solicitou acesso, apenas apague este email.</p>
                        </td>
                    </tr>
                    <!-- end permission -->

                    </table>
                    <!--[if (gte mso 9)|(IE)]>
                    </td>
                    </tr>
                    </table>
                    <![endif]-->
                </td>
                </tr>
                <!-- end footer -->

            </table>
            <!-- end body -->

            </body>
            </html>
            ";
        return email;
    }

    #endregion


}
