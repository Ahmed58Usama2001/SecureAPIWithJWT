﻿using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Secure_API_With_JWT.Helpers;
using Secure_API_With_JWT.Models;
using Secure_API_With_JWT.Services;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace TestApiJWT.Services;

public class AuthService : IAuthService
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly RoleManager<IdentityRole> _roleManager;
    private readonly JWT _jwt;

    public AuthService(UserManager<ApplicationUser> userManager, RoleManager<IdentityRole> roleManager, IOptions<JWT> jwt)
    {
        _userManager = userManager;
        _roleManager = roleManager;
        _jwt = jwt.Value;
    }

    public async Task<AuthModel> RegisterAsync(RegisterModel model)
    {
        if (await _userManager.FindByEmailAsync(model.Email) is not null)
            return new AuthModel { Message = "Email is already registered!" };

        if (await _userManager.FindByNameAsync(model.UserName) is not null)
            return new AuthModel { Message = "Username is already registered!" };

        var user = new ApplicationUser
        {
            UserName = model.UserName,
            Email = model.Email,
            FirstName = model.FirstName,
            LastName = model.LastName
        };

        var result = await _userManager.CreateAsync(user, model.Password);

        if (!result.Succeeded)
        {
            var errors = string.Empty;

            foreach (var error in result.Errors)
                errors += $"{error.Description},";

            return new AuthModel { Message = errors };
        }

        await _userManager.AddToRoleAsync(user, "User");

        var jwtSecurityToken = await CreateJwtToken(user);

        return new AuthModel
        {
            Email = user.Email,
            //ExpiresOn = jwtSecurityToken.ValidTo,
            IsAuthinticated = true,
            Roles = new List<string> { "User" },
            Token = new JwtSecurityTokenHandler().WriteToken(jwtSecurityToken),
            UserName = user.UserName
        };
    }

    public async Task<AuthModel> GetTokenAsync(TokenRequestModel model)
    {
        var authModel = new AuthModel();

        var user = await _userManager.FindByEmailAsync(model.Email);

        if (user is null || !await _userManager.CheckPasswordAsync(user, model.Password))
        {
            authModel.Message = "Email or Password is incorrect!";
            return authModel;
        }

        var jwtSecurityToken = await CreateJwtToken(user);
        var rolesList = await _userManager.GetRolesAsync(user);

        authModel.IsAuthinticated = true;
        authModel.Token = new JwtSecurityTokenHandler().WriteToken(jwtSecurityToken);
        authModel.Email = user.Email;
        authModel.UserName = user.UserName;
        //authModel.ExpiresOn = jwtSecurityToken.ValidTo;
        authModel.Roles = rolesList.ToList();

        if(user.RefreshTokens.Any(t=>t.IsActive))
        {
            var refreshToken = user.RefreshTokens.FirstOrDefault(t=>t.IsActive);
            authModel.RefreshToken = refreshToken.Token;
            authModel.RefreshTokenExpiration = (DateTime)refreshToken.ExpiresOn;
        }
        else
        {
            var refreshToken = GenerateRefreshToken();
            authModel.RefreshToken = refreshToken.Token;
            authModel.RefreshTokenExpiration = (DateTime)refreshToken.ExpiresOn;
            user.RefreshTokens.Add(refreshToken);
            await _userManager.UpdateAsync(user);
        }

        return authModel;
    }

    public async Task<string> AddRoleAsync(AddRoleModel model)
    {
        var user = await _userManager.FindByIdAsync(model.UserId);

        if (user is null || !await _roleManager.RoleExistsAsync(model.Role))
            return "Invalid user ID or Role";

        if (await _userManager.IsInRoleAsync(user, model.Role))
            return "User already assigned to this role";

        var result = await _userManager.AddToRoleAsync(user, model.Role);

        return result.Succeeded ? string.Empty : "Sonething went wrong";
    }

    private async Task<JwtSecurityToken> CreateJwtToken(ApplicationUser user)
    {
        var userClaims = await _userManager.GetClaimsAsync(user);
        var roles = await _userManager.GetRolesAsync(user);
        var roleClaims = new List<Claim>();

        foreach (var role in roles)
            roleClaims.Add(new Claim("roles", role));

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.UserName),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new Claim(JwtRegisteredClaimNames.Email, user.Email),
            new Claim("uid", user.Id)
        }
        .Union(userClaims)
        .Union(roleClaims);

        var symmetricSecurityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwt.key));
        var signingCredentials = new SigningCredentials(symmetricSecurityKey, SecurityAlgorithms.HmacSha256);

        var jwtSecurityToken = new JwtSecurityToken(
            issuer: _jwt.Issuer,
            audience: _jwt.Audience,
            claims: claims,
            expires: DateTime.Now.AddDays(_jwt.DurationInDays),
            signingCredentials: signingCredentials);

        return jwtSecurityToken;
    }

    private RefreshToken GenerateRefreshToken()
    {
        var token = new byte[32];

        using var generator =new RNGCryptoServiceProvider();

        generator.GetBytes(token);

        return new RefreshToken
        {
            Token=Convert.ToBase64String(token),
            ExpiresOn=DateTime.UtcNow.AddDays(5),
            CreatedOn=DateTime.UtcNow,
        };

    }

    public async Task<AuthModel> RefreshTokenAsync(string token)
    {
        AuthModel authModel=new AuthModel();


        var user = await _userManager.Users.SingleOrDefaultAsync(u => u.RefreshTokens.Any(t => t.Token == token));

        if(user is null)
        {   
            authModel.IsAuthinticated = false;
            authModel.Message = "Invalid token";

            return authModel;
        }

        var refreshToken= user.RefreshTokens.Single(t=>t.Token == token);

        if(!refreshToken.IsActive)
        {
            authModel.IsAuthinticated = false;
            authModel.Message = "Inactive token";

            return authModel;
        }

        refreshToken.RevokedOn= DateTime.UtcNow;

        var newRefreshToken=GenerateRefreshToken();
        user.RefreshTokens.Add(newRefreshToken);
       await _userManager.UpdateAsync(user);

        var jwtToken = await CreateJwtToken(user);
        authModel.Email = user.Email;
        authModel.UserName = user.UserName;
        authModel.RefreshToken = newRefreshToken.Token;
        authModel.IsAuthinticated = true;
        authModel.RefreshTokenExpiration= (DateTime)newRefreshToken.ExpiresOn;
        authModel.Token=new JwtSecurityTokenHandler().WriteToken(jwtToken);
        var roles=await _userManager.GetRolesAsync(user);
        authModel.Roles = roles.ToList();


        return authModel;
    }

    public async Task<bool> RevokeTokenAsync(string token)
    {
        var user = await _userManager.Users.SingleOrDefaultAsync(u => u.RefreshTokens.Any(t => t.Token == token));

        if (user == null)
            return false;

        var refreshToken = user.RefreshTokens.Single(t => t.Token == token);

        if (!refreshToken.IsActive)
            return false;

        refreshToken.RevokedOn = DateTime.UtcNow;

        await _userManager.UpdateAsync(user);

        return true;
    }
}