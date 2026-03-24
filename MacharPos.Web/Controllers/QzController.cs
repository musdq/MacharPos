using Microsoft.AspNetCore.Mvc;
using System.Security.Cryptography;
using System.Text;

[ApiController]
public class QzController : ControllerBase
{
    private readonly IWebHostEnvironment _env;

    public QzController(IWebHostEnvironment env)
    {
        _env = env;
    }

    [HttpGet("/qz/digital-certificate.txt")]
    public IActionResult Certificate()
    {
        var path = Path.Combine(_env.ContentRootPath, "qz", "digital-certificate.txt");
        return PhysicalFile(path, "text/plain");
    }

    [HttpGet("/qz/sign")]
    public IActionResult Sign(string request)
    {
        var keyPath = Path.Combine(_env.ContentRootPath, "qz", "private-key.pem");
        var pem = System.IO.File.ReadAllText(keyPath);

        using var rsa = RSA.Create();
        rsa.ImportFromPem(pem);

        var signature = rsa.SignData(
            Encoding.UTF8.GetBytes(request),
            HashAlgorithmName.SHA512,
            RSASignaturePadding.Pkcs1
        );

        return Content(Convert.ToBase64String(signature));
    }
}