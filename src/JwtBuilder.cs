using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Cryptography;

namespace orgbranchprotection
{
    public class JwtBuilder : IDisposable
    {
        private RSA _rsa;
        private readonly byte[] _privateKey;
        private readonly string _iss;
        private bool disposedValue;

        public JwtBuilder(byte[] privateKey, string iss)
        {
            _privateKey = privateKey;
            _iss = iss;
            _rsa = RSA.Create();
        }

        public string GetToken()
        {
            _rsa.ImportRSAPrivateKey(_privateKey, out _);

            var jwtHandler = new JwtSecurityTokenHandler();

            var descriptor = new SecurityTokenDescriptor
            {
                Issuer = _iss,
                IssuedAt = DateTime.UtcNow.AddSeconds(-30),
                Expires = DateTime.UtcNow.AddMinutes(10),
                SigningCredentials = new SigningCredentials(new RsaSecurityKey(_rsa) { KeyId = "" }, SecurityAlgorithms.RsaSha256)
            };

            var jwtToken = jwtHandler.CreateToken(descriptor);
            var b64token = jwtHandler.WriteToken(jwtToken);

            return b64token;
        }
        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                    _rsa.Dispose();

                disposedValue = true;
            }
        }

        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}