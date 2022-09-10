using System.Collections.Generic;
using RoR2;

namespace Archipelago.RiskOfRain2.Handlers
{
    class StageBlockerHandler : IHandler
    {
        // setup all scene indexes as megic numbers
        // scenes from https://risk-of-thunder.github.io/R2Wiki/Mod-Creation/Developer-Reference/Scene-Names/
        // main scenes
        public const int ancientloft = 3;       // Aphelian Sanctuary
        public const int arena = 4;             // Void Fields
        public const int blackbeach = 7;        // Distant Roost
        public const int blackbeach2 = 8;       // Distant Roost
        public const int dampcavesimple = 10;   // Abyssal Depths
        public const int foggyswamp = 12;       // Wetland Aspect
        public const int frozenwall = 13;       // Rallypoint Delta
        public const int golemplains = 15;      // Titanic Plains
        public const int golemplains2 = 16;     // Titanic Plains
        public const int goolake = 17;          // Abandoned Aqueduct
        public const int itancientloft = 20;    // The Simulacrum
        public const int itdampcave = 21;       // The Simulacrum
        public const int itfrozenwall = 22;     // The Simulacrum
        public const int itgolemplains = 23;    // The Simulacrum
        public const int itgoolake = 24;        // The Simulacrum
        public const int itmoon = 25;           // The Simulacrum
        public const int itskymeadow = 26;      // The Simulacrum
        public const int moon2 = 32;            // Commencement
        public const int rootjungle = 35;       // Sundered Grove
        public const int shipgraveyard = 37;    // Siren's Call
        public const int skymeadow = 38;        // Sky Meadow
        public const int snowyforest = 39;      // Siphoned Forest
        public const int sulfurpools = 41;      // Sulfur Pools
        public const int voidstage = 45;        // Void Locus
        public const int voidraid = 46;         // The Planetarium
        public const int wispgraveyard = 47;    // Scorched Acres
        // hidden realms
        public const int artifactworld = 5;     // Hidden Realm: Bulwark's Ambry
        public const int bazaar = 6;            // Hidden Realm: Bazaar Between Time
        public const int goldshores = 14;       // Hidden Realm: Gilded Coast
        public const int limbo = 27;            // Hidden Realm: A Moment, Whole
        public const int mysteryspace = 33;     // Hidden Realm: A Moment, Fractured


        // A list of stages that should be blocked because they are locked by archipelago
        // uses scene names: https://risk-of-thunder.github.io/R2Wiki/Mod-Creation/Developer-Reference/Scene-Names/
        List<int> blocked_stages;

        public StageBlockerHandler()
        {
            // TODO figure out what stages to block from the settings or YAML or something
            blocked_stages = new List<int>();

            BlockAll();
            // TODO make the server push the precollected first stage

            // TODO block portals for dimensions that cannot be traveld to from spawning
            // TODO block bazar shop from purchasing environments that should be blocked

            // unblock some stages for testing
            // XXX remove this stuff
            //UnBlock(blackbeach);
            //UnBlock(goolake);
            //UnBlock(frozenwall);
        }

        public void Hook()
        {
            On.RoR2.Run.CanPickStage += Run_CanPickStage;
        }

        public void UnHook()
        {
            On.RoR2.Run.CanPickStage -= Run_CanPickStage;
        }

        public void BlockAll()
        {
            // scenes from https://risk-of-thunder.github.io/R2Wiki/Mod-Creation/Developer-Reference/Scene-Names/
            // block all main scenes
            Block(ancientloft);        // Aphelian Sanctuary
            Block(arena);              // Void Fields
            Block(blackbeach);         // Distant Roost
            Block(blackbeach2);        // Distant Roost
            Block(dampcavesimple);     // Abyssal Depths
            Block(foggyswamp);         // Wetland Aspect
            Block(frozenwall);         // Rallypoint Delta
            Block(golemplains);        // Titanic Plains
            Block(golemplains2);       // Titanic Plains
            Block(goolake);            // Abandoned Aqueduct
            Block(itancientloft);      // The Simulacrum
            Block(itdampcave);         // The Simulacrum
            Block(itfrozenwall);       // The Simulacrum
            Block(itgolemplains);      // The Simulacrum
            Block(itgoolake);          // The Simulacrum
            Block(itmoon);             // The Simulacrum
            Block(itskymeadow);        // The Simulacrum
            Block(moon2);              // Commencement
            Block(rootjungle);         // Sundered Grove
            Block(shipgraveyard);      // Siren's Call
            Block(skymeadow);          // Sky Meadow
            Block(snowyforest);        // Siphoned Forest
            Block(sulfurpools);        // Sulfur Pools
            Block(voidstage);          // Void Locus
            Block(voidraid);           // The Planetarium
            Block(wispgraveyard);      // Scorched Acres
            // block all hidden realms
            Block(artifactworld);      // Hidden Realm: Bulwark's Ambry
            Block(bazaar);             // Hidden Realm: Bazaar Between Time
            Block(goldshores);         // Hidden Realm: Gilded Coast
            Block(limbo);              // Hidden Realm: A Moment, Whole
            Block(mysteryspace);       // Hidden Realm: A Moment, Fractured
        }

        public void UnBlockAll()
        {
            blocked_stages.Clear();
        }

        /**
         * Blocks a given environment.
         * Returns true if the stage was blocked by this call.
         */
        public bool Block(int index)
        {
            if (blocked_stages.Contains(index)) return false;
            Log.LogDebug($"Blocking {index}."); // XXX remove extra debug
            blocked_stages.Add(index);
            return true;
        }

        /**
         * Unblocks a given environment.
         * Returns true if the stage was unblocked by this call.
         */
        public bool UnBlock(int index)
        {
            // TODO the initial unblock will occur after the game starts the first stage, this should be fixed
            Log.LogDebug($"UnBlocking {index}."); // XXX remove extra debug
            return blocked_stages.Remove(index);
        }

        /**
         * Unblocks a given environment.
         * Uses the English Titles found here: https://risk-of-thunder.github.io/R2Wiki/Mod-Creation/Developer-Reference/Scene-Names/
         * For environments with 2 varients, the second varient has " (2)" appended to the name.
         * For simulacrum, the stages have the non-simulacrum name appended in parenthesis.
         * Returns true if the stage was unblocked by this call.
         */
        public bool UnBlock(string environmentname)
        {
            Log.LogDebug($"UnBlocking {environmentname}."); // XXX remove extra debug
            switch (environmentname)
            {
                case "Aphelian Sanctuary":
                    return UnBlock(3); // ancientloft
                case "Void Fields":
                    return UnBlock(4); // arena
                case "Distant Roost":
                    return UnBlock(7); // blackbeach
                case "Distant Roost (2)":
                    return UnBlock(8); // blackbeach2
                case "Abyssal Depths":
                    return UnBlock(10); // dampcavesimple
                case "Wetland Aspect":
                    return UnBlock(12); // foggyswamp
                case "Rallypoint Delta":
                    return UnBlock(13); // frozenwall
                case "Titanic Plains":
                    return UnBlock(15); // golemplains
                case "Titanic Plains (2)":
                    return UnBlock(16); // golemplains2
                case "Abandoned Aqueduct":
                    return UnBlock(17); // goolake
                case "The Simulacrum (Aphelian Sanctuary)":
                    return UnBlock(20); // itancientloft
                case "The Simulacrum (Abyssal Depths)":
                    return UnBlock(21); // itdampcave
                case "The Simulacrum (Rallypoint Delta)":
                    return UnBlock(22); // itfrozenwall
                case "The Simulacrum (Titanic Plains)":
                    return UnBlock(23); // itgolemplains
                case "The Simulacrum (Abandoned Aqueduct)":
                    return UnBlock(24); // itgoolake
                case "The Simulacrum (Commencement)":
                    return UnBlock(25); // itmoon
                case "The Simulacrum (Sky Meadow)":
                    return UnBlock(26); // itskymeadow
                case "Commencement":
                    return UnBlock(32); // moon2
                case "Sundered Grove":
                    return UnBlock(35); // rootjungle
                case "Siren's Call":
                    return UnBlock(37); // shipgraveyard
                case "Sky Meadow":
                    return UnBlock(38); // skymeadow
                case "Siphoned Forest":
                    return UnBlock(39); // snowyforest
                case "Sulfur Pools":
                    return UnBlock(41); // sulfurpools
                case "Void Locus":
                    return UnBlock(45); // voidstage
                case "The Planetarium":
                    return UnBlock(46); // voidraid
                case "Scorched Acres":
                    return UnBlock(47); // wispgraveyard
                case "Hidden Realm: Bulwark's Ambry":
                    return UnBlock(5); // artifactworld
                case "Hidden Realm: Bazaar Between Time":
                    return UnBlock(6); // bazaar
                case "Hidden Realm: Gilded Coast":
                    return UnBlock(14); // goldshores
                case "Hidden Realm: A Moment, Whole":
                    return UnBlock(27); // limbo
                case "Hidden Realm: A Moment, Fractured":
                    return UnBlock(33); // mysteryspace
                default:
                    return false;
            }
        }

        private bool Run_CanPickStage(On.RoR2.Run.orig_CanPickStage orig, Run self, SceneDef scenedef)
        {
            Log.LogDebug($"Checking CanPickStage for {scenedef.nameToken}..."); // XXX remove extra debug
            int index = (int) scenedef.sceneDefIndex;
            foreach (int block in blocked_stages)
            {
                // if the stage is blocked, it cannot be picked
                if (index == block)
                {
                    Log.LogDebug("blocking."); // XXX remove extra debug
                    return false;
                }
            }

            Log.LogDebug("passing through."); // XXX remove extra debug

            return orig(self, scenedef);
        }

    }
}
