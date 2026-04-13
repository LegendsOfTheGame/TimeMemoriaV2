using System.Collections.Generic;

namespace TimeMemoria
{
    internal static class ArrStartingPaths
    {
        public static readonly HashSet<uint> Gridania = new()
        {
            65621, 65659, 65660, 65564, 65737, 65981, 65664, 69390, 65711,
            65661, 69391, 65665, 65712, 65912, 65913, 65915, 65916, 65917,
            65920, 65923, 65697, 65982, 65983, 65984, 65985, 66043,
            66210,
        };

        public static readonly HashSet<uint> Limsa = new()
        {
            65644, 65645, 65998, 65999, 66079, 66001, 66002, 66003, 66004,
            66005, 65933, 65938, 65939, 65942, 65948, 65951, 65949, 65950,
            66225, 66080, 66226, 66081, 66082,
            66210,
        };

        public static readonly HashSet<uint> Uldah = new()
        {
            66104, 66105, 66106, 66131, 66207, 66086, 65839, 65842, 69388,
            65843, 65856, 66159, 65864, 66039, 65865, 65866, 65867, 69389,
            65868, 65869, 65870, 65872, 66164, 66087, 66177, 66088, 66064,
            66209,
        };

        // Returns true if ALL of this quest's IDs belong exclusively to
        // paths the player did NOT take. A quest is only excluded when
        // none of its IDs appear in the player's own path set.
        public static bool IsExcludedForStartArea(List<uint> questIds, string startArea)
        {
            var playerPath = startArea switch
            {
                "Gridania"      => Gridania,
                "Limsa Lominsa" => Limsa,
                "Ul'dah"        => Uldah,
                _               => null
            };

            if (playerPath == null) return false;

            // If any ID in this quest belongs to the player's path, keep it
            foreach (var id in questIds)
            {
                if (playerPath.Contains(id)) return false;
            }

            // If any ID belongs to one of the OTHER paths, exclude it
            foreach (var id in questIds)
            {
                if ((!playerPath.Contains(id)) &&
                    (Gridania.Contains(id) || Limsa.Contains(id) || Uldah.Contains(id)))
                    return true;
            }

            return false;
        }
    }
}
