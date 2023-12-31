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
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;

namespace uklon_backend.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class LoginController : ControllerBase
    {
        private readonly IConfiguration _configuration;
        private readonly UserManager<User> userManager;
        private readonly SignInManager<User> signInManager;
        private readonly IWebHostEnvironment _hostingEnvironment;
        private readonly UklonDbContext _context;

        public LoginController(IConfiguration configuration,
                              UserManager<User> userManager,
                              SignInManager<User> signInManager,
                              UklonDbContext context,
                              IWebHostEnvironment hostingEnvironment)
        {
            this.userManager = userManager;
            this.signInManager = signInManager;
            _configuration = configuration;
            _context = context;
            _hostingEnvironment = hostingEnvironment;
        }

        [HttpPost("login-phone")]
        public async Task<IActionResult> LoginPhoneAsync(PhoneNumberVerificationDto phoneNumberDto)
        {
            // Отримати номер телефона користувача з phoneNumberDto
            string phoneNumber = phoneNumberDto.PhoneNumber;

            var user = await userManager.FindByNameAsync(phoneNumber);
            bool isPassword = await userManager.CheckPasswordAsync(user, phoneNumberDto.Password);

            if (user != null && isPassword)
            {
                // return BadRequest();
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

        [HttpPost("login-email")]
        public async Task<IActionResult> LoginEmailAsync(PhoneNumberVerificationDto phoneNumberDto)
        {
            // Отримати номер телефона користувача з phoneNumberDto
            string emailAddress = phoneNumberDto.Email;

            var user = await userManager.FindByNameAsync(emailAddress);
            bool isPassword = await userManager.CheckPasswordAsync(user, phoneNumberDto.Password);

            if (user != null && isPassword)
            {
                // return BadRequest();
            }
            if (user == null)
            {
                user = new User()
                {
                    UserName = emailAddress,
                    Email = emailAddress,
                    RoleId = "Client",
                    PasswordHash = phoneNumberDto.Password
                };

                await userManager.CreateAsync(user, phoneNumberDto.Password);
                _context.Users.Add(user);
            }

            // Генерація JWT-токена
            var token = GenerateJwtTokenAsync(emailAddress, user);

            user.Token = await token;

            await signInManager.SignInAsync(user, true);

            _context.SaveChanges();

            // Повернути JWT-токен відповідь
            return Ok(new { Token = token });
        }

        [HttpPost("found-or-create-user")]
        public async Task<IActionResult> FoundCreateAsync(UserDTO user)
        {
            User foundUser;
            string normEmail = userManager.NormalizeEmail(user.Email);

            foundUser = await userManager.FindByEmailAsync(normEmail);
            if (foundUser != null)
            {
                return Ok(foundUser);
            }
            else
            {
                foundUser = new User()
                {
                    FirstName = user.FirstName,
                    LastName = user.LastName,
                    UserName = user.FirstName + " " + user.LastName,
                    NormalizedUserName = userManager.NormalizeName(user.FirstName + " " + user.LastName),
                    Email = user.Email,
                    NormalizedEmail = userManager.NormalizeEmail(user.Email),
                    PasswordHash = await ComputeSHA256Hash(user.Password),
                    RoleId = "Client",
                    PhoneNumber = user.PhoneNumber
                };


                foundUser.Token = await GenerateJwtTokenAsync(user.PhoneNumber, foundUser);

                _context.Users.Add(foundUser);
                _context.SaveChanges(true);
                return Ok(foundUser);
            }
        }

        [HttpPut("update-user/{id}")]
        public async Task<IActionResult> UpdateUserAsync(UserDTO user, string id)
        {
            try
            {
                // Пошук моделі за ідентифікатором
                var existingModel = await _context.Users.FindAsync(id);

                if (existingModel == null)
                    return NotFound();

                // Оновлення полів моделі
                existingModel.FirstName = user.FirstName;
                existingModel.LastName = user.LastName;
                existingModel.PhoneNumber = user.PhoneNumber;
                existingModel.Email = user.Email;
                existingModel.Url = user.Url;

                // Зберегти зміни
                await _context.SaveChangesAsync();

                return Ok(existingModel);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Помилка при оновленні даних: {ex.Message}");
            }
        }

        [HttpGet("get-users")]
        public async Task<ActionResult<IEnumerable<User>>> GetUsers()
        {
            var users = await _context.Users.ToListAsync();
            return Ok(users);
        }

        [HttpPut("register-driver")]
        public async Task<IActionResult> RegisterDriver(UserDTO user)
        {
            User foundUser;
            string normEmail = userManager.NormalizeEmail(user.Email);

            foundUser = await userManager.FindByEmailAsync(normEmail);
            if (foundUser != null)
            {
                try
                {
                    // Пошук моделі за ідентифікатором
                    var existingModel = await _context.Users.FindAsync(foundUser.Id);

                    if (existingModel == null)
                        return NotFound();

                    // Оновлення полів моделі
                    existingModel.RoleId = "Driver";

                    // Зберегти зміни
                    await _context.SaveChangesAsync();

                    return Ok(existingModel);
                }
                catch (Exception ex)
                {
                    return StatusCode(500, $"Помилка при оновленні даних: {ex.Message}");
                }
            }
            else
            {
                return BadRequest();
            }
        }

        [HttpPut("register-corp")]
        public async Task<IActionResult> RegisterCorp(UserDTO user)
        {
            User foundUser;
            string normEmail = userManager.NormalizeEmail(user.Email);

            foundUser = await userManager.FindByEmailAsync(normEmail);
            if (foundUser != null)
            {
                try
                {
                    var existingModel = await _context.Users.FindAsync(foundUser.Id);

                    if (existingModel == null)
                        return NotFound();

                    existingModel.RoleId = "Corporation";

                    await _context.SaveChangesAsync();

                    return Ok(existingModel);
                }
                catch (Exception ex)
                {
                    return StatusCode(500, $"Помилка при оновленні даних: {ex.Message}");
                }
            }
            else
            {
                return BadRequest();
            }
        }

        [HttpPut("register-part")]
        public async Task<IActionResult> RegisterPart(UserDTO user)
        {
            User foundUser;
            string normEmail = userManager.NormalizeEmail(user.Email);

            foundUser = await userManager.FindByEmailAsync(normEmail);
            if (foundUser != null)
            {
                try
                {
                    var existingModel = await _context.Users.FindAsync(foundUser.Id);

                    if (existingModel == null)
                        return NotFound();

                    existingModel.RoleId = "Partner";

                    await _context.SaveChangesAsync();

                    return Ok(existingModel);
                }
                catch (Exception ex)
                {
                    return StatusCode(500, $"Помилка при оновленні даних: {ex.Message}");
                }
            }
            else
            {
                return BadRequest();
            }
        }

        [HttpPut("change-password")]
        public async Task<IActionResult> ChangePass(ChangeDTO change)
        {
            User user = await userManager.FindByIdAsync(change.userId);

            try
            {
                // Пошук моделі за ідентифікатором
                var existingModel = await _context.Users.FindAsync(change.userId);

                if (existingModel == null)
                    return NotFound();

                // Оновлення полів моделі
                existingModel.PasswordHash = await ComputeSHA256Hash(change.password);

                // Зберегти зміни
                await _context.SaveChangesAsync();

                return Ok(existingModel);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Помилка при оновленні даних: {ex.Message}");
            }
        }

        [HttpPost("upload-photo")]
        public async Task<IActionResult> UploadPhotoAsync(IFormFile imageFile)
        {
            if (imageFile == null || imageFile.Length == 0)
            {
                return BadRequest("No file uploaded.");
            }

            try
            {
                var fileName = Guid.NewGuid().ToString() + Path.GetExtension(imageFile.FileName);

                // Шлях для збереження файлу на сервері
                var filePath = Path.Combine("uploads", fileName);

                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await imageFile.CopyToAsync(stream);
                }

                // Ви можете зберігати шлях до файлу в базі даних або повертати його як відповідь
                string imageUrl = "/uploads/" + fileName;

                return Ok(fileName);
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }

        [HttpGet("api/photos/{photoId}")]
        public IActionResult GetPhoto(string photoId)
        {
            // Шукайте фотографію за її ідентифікатором photoId в базі даних або на сервері.
            // Отримайте її байти.
            byte[] photoBytes = GetPhotoBytes(photoId);

            if (photoBytes == null)
            {
                return NotFound(); // Фотографію не знайдено, поверніть 404 Not Found.
            }

            // Встановлюйте заголовки відповіді для вказівки типу контенту.
            // У цьому випадку, це зображення png.
            Response.Headers.Add("Content-Type", "image/png");

            // Поверніть фотографію як байти.
            return File(photoBytes, "image/png");
        }

        private byte[] GetPhotoBytes(string photoId)
        {
            // Спочатку складіть повний шлях до файлу фотографії на сервері.
            var filePath = Path.Combine("uploads", photoId);

            // Перевірте, чи існує файл за вказаним шляхом.
            if (System.IO.File.Exists(filePath))
            {
                // Якщо файл існує, прочитайте його байти.
                return System.IO.File.ReadAllBytes(filePath);
            }

            // Якщо файл не існує, поверніть null або порожній масив, якщо потрібно.
            return null;
        }

        private async Task<string> ComputeSHA256Hash(string password)
        {

            using (SHA256 sha256 = SHA256.Create())
            {
                byte[] passwordBytes = Encoding.UTF8.GetBytes(password);
                byte[] hashBytes = sha256.ComputeHash(passwordBytes);
                string hashedPassword = BitConverter.ToString(hashBytes).Replace("-", string.Empty).ToLower();
                return hashedPassword;
            }
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
