﻿using BissnesLogic.DTOs;
using BissnesLogic.Entites;
using BissnesLogic.Settings;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Net.Mail;
using System.Net;
using System.Security.Claims;
using System.Text;
using Data;
using Inetlab.SMPP;
using Inetlab.SMPP.PDU;

namespace uklon_backend.Controllers
{
    [Route("api/[controller]")]
    [ApiController]

    public class RegisterControler : ControllerBase
    {
        private readonly IConfiguration _configuration;
        private readonly UserManager<User> userManager;
        private readonly SignInManager<User> signInManager;
        private readonly UklonDbContext _context;

        public RegisterControler(IConfiguration configuration,
                              UserManager<User> userManager,
                              SignInManager<User> signInManager,
                              UklonDbContext context)
        {
            this.userManager = userManager;
            this.signInManager = signInManager;
            _configuration = configuration;
            _context = context;
        }

        [HttpPost("reg-phone")]
        public async Task<IActionResult> RegisterPhoneAsync(PhoneNumberVerificationDto phoneNumberDto)
        {
            // Отримати номер телефона користувача з phoneNumberDto
            string phoneNumber = phoneNumberDto.PhoneNumber;

            var user = await userManager.FindByNameAsync(phoneNumber);
            bool isPassword = await userManager.CheckPasswordAsync(user, phoneNumberDto.Password);

            if (user != null && !isPassword)
            {
                return BadRequest();
            }
            if (user == null)
            {
                user = new User()
                {
                    UserName = phoneNumber,
                    PhoneNumber = phoneNumber,
                    RoleId = "Client",
                    PasswordHash = phoneNumberDto.Password
                };

                await userManager.CreateAsync(user, phoneNumberDto.Password);
                _context.Users.Add(user);
            }

            // Генерація JWT-токена
            var token = GenerateJwtTokenAsync(phoneNumber, user);

            user.Token = await token;

            await signInManager.SignInAsync(user, true);

            _context.SaveChanges();

            // Повернути JWT-токен відповідь
            return Ok(new { Token = token });
        }

        private async Task<string> GenerateJwtTokenAsync(string phoneNumber, User user)
        {
            // Отримати параметри JWT-токена з конфігурації
            var jwtSettings = _configuration.GetSection("JwtSettings").Get<JwtSettings>();
            var secretKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings.SecretKey));
            var signingCredentials = new SigningCredentials(secretKey, SecurityAlgorithms.HmacSha256);

            // Задати клейми для JWT-токена (якщо потрібно)
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.Name, phoneNumber)
            };

            var roles = await userManager.GetRolesAsync(user);
            foreach (var role in roles)
            {
                claims.Add(new Claim(ClaimTypes.Role, role));
            }

            // Створити JWT-токен
            var tokenOptions = new JwtSecurityToken(
                issuer: jwtSettings.Issuer,
                audience: jwtSettings.Audience,
                claims: claims,
                expires: DateTime.UtcNow.AddMinutes(jwtSettings.ExpirationMinutes),
                signingCredentials: signingCredentials
            );

            // Згенерувати строкове представлення JWT-токена
            var tokenString = new JwtSecurityTokenHandler().WriteToken(tokenOptions);

            return tokenString;
        }

    }
}
