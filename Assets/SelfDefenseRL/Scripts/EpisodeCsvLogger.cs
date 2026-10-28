using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

namespace SelfDefenseRL
{
    /// <summary>
    /// Schreibt jede Episode als Zeile in eine CSV-Datei (für die Auswertung in Python).
    /// Pfad: Application.persistentDataPath/selfdefense_episodes.csv (wird in der Konsole ausgegeben).
    /// </summary>
    public static class EpisodeCsvLogger
    {
        static string path;
        static readonly object fileLock = new object();

        public static string FilePath
        {
            get
            {
                if (path == null)
                {
                    path = Path.Combine(Application.persistentDataPath, "selfdefense_episodes.csv");
                    Debug.Log($"[SelfDefenseRL] Episoden-Log: {path}");
                }
                return path;
            }
        }

        public static void Log(CombatSimulation sim)
        {
            var inv = CultureInfo.InvariantCulture;
            lock (fileLock)
            {
                bool newFile = !File.Exists(FilePath);
                var sb = new StringBuilder();
                if (newFile)
                {
                    sb.Append("scenario,style,mass_ratio,strength_ratio,success,end_reason,time,health_left,attacker_damage,damage_taken,times_grabbed,takedowns,time_pinned_wall,first_release_time,decisive_technique,dirty_tricks");
                    for (int i = 1; i < TechniqueLibrary.Count; i++) sb.Append(",uses_").Append(TechniqueLibrary.All[i].Name);
                    for (int i = 1; i < TechniqueLibrary.Count; i++) sb.Append(",hits_").Append(TechniqueLibrary.All[i].Name);
                    for (int i = 1; i < TechniqueLibrary.Count; i++) sb.Append(",releases_").Append(TechniqueLibrary.All[i].Name);
                    for (int i = 1; i < TechniqueLibrary.Count; i++) sb.Append(",knockdowns_").Append(TechniqueLibrary.All[i].Name);
                    for (int i = 1; i < TechniqueLibrary.Count; i++) sb.Append(",wallturns_").Append(TechniqueLibrary.All[i].Name);
                    sb.Append('\n');
                }
                sb.Append(sim.Scenario).Append(',')
                  .Append(sim.Style).Append(',')
                  .Append(sim.MassRatio.ToString("0.###", inv)).Append(',')
                  .Append(sim.StrengthRatio.ToString("0.###", inv)).Append(',')
                  .Append(sim.Success ? 1 : 0).Append(',')
                  .Append(sim.EndReason.Replace(',', ' ')).Append(',')
                  .Append(sim.Time.ToString("0.##", inv)).Append(',')
                  .Append(Mathf.Max(0f, sim.DefHealth).ToString("0.###", inv)).Append(',')
                  .Append(sim.AttDamage.ToString("0.###", inv)).Append(',')
                  .Append(sim.DamageTakenTotal.ToString("0.###", inv)).Append(',')
                  .Append(sim.TimesGrabbed).Append(',')
                  .Append(sim.Takedowns).Append(',')
                  .Append(sim.TimePinnedToWall.ToString("0.##", inv)).Append(',')
                  .Append(sim.FirstReleaseTime.ToString("0.##", inv)).Append(',')
                  .Append(sim.Success ? TechniqueLibrary.All[sim.DecisiveTechnique].Name : "").Append(',')
                  .Append(sim.S.DirtyTricksForAll ? 1 : 0);
                for (int i = 1; i < TechniqueLibrary.Count; i++) sb.Append(',').Append(sim.TechniqueUses[i]);
                for (int i = 1; i < TechniqueLibrary.Count; i++) sb.Append(',').Append(sim.TechniqueHits[i]);
                for (int i = 1; i < TechniqueLibrary.Count; i++) sb.Append(',').Append(sim.TechniqueReleases[i]);
                for (int i = 1; i < TechniqueLibrary.Count; i++) sb.Append(',').Append(sim.TechniqueKnockdowns[i]);
                for (int i = 1; i < TechniqueLibrary.Count; i++) sb.Append(',').Append(sim.TechniqueWallTurns[i]);
                sb.Append('\n');
                File.AppendAllText(FilePath, sb.ToString());
            }
        }
    }
}
