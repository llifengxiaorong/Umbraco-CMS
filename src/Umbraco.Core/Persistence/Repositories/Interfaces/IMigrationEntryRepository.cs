﻿using System;
using Umbraco.Core.Models;

namespace Umbraco.Core.Persistence.Repositories
{
    public interface IMigrationEntryRepository : IRepositoryQueryable<int, IMigrationEntry>
    {
        IMigrationEntry FindEntry(string migrationName, Version version);
    }
}