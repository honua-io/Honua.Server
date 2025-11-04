// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
﻿namespace Honua.Server.Core.Authentication;

public interface IPasswordHasher
{
    PasswordHashResult HashPassword(string password);

    bool VerifyPassword(string password, byte[] hash, byte[] salt, string algorithm, string parameters);
}
