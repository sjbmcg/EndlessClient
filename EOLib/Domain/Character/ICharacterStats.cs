﻿// Original Work Copyright (c) Ethan Moffat 2014-2016
// This file is subject to the GPL v2 License
// For additional details, see the LICENSE file

using System.Collections.Generic;

namespace EOLib.Domain.BLL
{
	public interface ICharacterStats
	{
		IReadOnlyDictionary<CharacterStat, int> Stats { get; }

		int this[CharacterStat stat] { get; }

		ICharacterStats WithNewStat(CharacterStat whichStat, int statValue);
	}
}