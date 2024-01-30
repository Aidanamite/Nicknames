using HarmonyLib;
using SRML;
using SRML.Console;
using SRML.SR;
using SRML.SR.SaveSystem;
using SRML.SR.SaveSystem.Data;
using System.Reflection;
using UnityEngine;
using MonomiPark.SlimeRancher.DataModel;
using System.Collections.Generic;

namespace Nicknames
{
    public class Main : ModEntryPoint
    {
        internal static Assembly modAssembly = Assembly.GetExecutingAssembly();
        internal static string modName = $"{modAssembly.GetName().Name}";
        internal static string modDir = $"{System.Environment.CurrentDirectory}\\SRML\\Mods\\{modName}";
        internal static Dictionary<string, string> names = new Dictionary<string, string>();

        public override void PreLoad()
        {
            HarmonyInstance.PatchAll();
            SaveRegistry.RegisterDataParticipant<SlimeNickname>();
            Console.RegisterCommand(new NicknameCommand());
            SaveRegistry.RegisterWorldDataPreLoadDelegate(LoadWorldData);
            SaveRegistry.RegisterWorldDataSaveDelegate(SaveWorldData);
        }
        public override void PostLoad()
        {
            SRCallbacks.OnMainMenuLoaded += (x) => names.Clear();
        }
        public static void Log(string message) => Console.Log($"[{modName}]: " + message);
        public static void LogError(string message) => Console.LogError($"[{modName}]: " + message);
        public static void LogWarning(string message) => Console.LogWarning($"[{modName}]: " + message);
        public static void LogSuccess(string message) => Console.LogSuccess($"[{modName}]: " + message);

        static void LoadWorldData(CompoundDataPiece data)
        {
            names.Clear();
            if (!data.HasPiece("gordoNames"))
                return;
            var nameStore = data.GetCompoundPiece("gordoNames");
            foreach (var p in nameStore.DataList)
                names.Add(p.key, p.GetValue<string>());
        }

        static void SaveWorldData(CompoundDataPiece data)
        {
            var nameStore = new CompoundDataPiece("gordoNames");
            if (data.HasPiece("gordoNames"))
            {
                nameStore = data.GetCompoundPiece("gordoNames");
                nameStore.DataList.Clear();
            }
            else
                data.AddPiece(nameStore);
            foreach (var p in names)
                nameStore.AddPiece(new DataPiece(p.Key, p.Value));
        }
    }

    class NicknameCommand : ConsoleCommand
    {
        public override string Usage => "nickname [name]";
        public override string ID => "nickname";
        public override string Description => "Sets the nickname of the slime you're looking at";
        public override bool Execute(string[] args)
        {
            var g = SceneContext.Instance.PlayerState.Targeting;
            var flag = 0;
            if (g && Identifiable.IsSlime(Identifiable.GetId(g)))
                flag = 1;
            if (g && g.GetComponent<GordoIdentifiable>())
                flag = 2;
            if (flag == 0)
            {
                Main.LogError("No slime in line of sight");
                return true;
            }
            var n = g.GetComponent<Nickname>();
            if (args == null || args.Length < 1)
            {
                if (n)
                    Object.DestroyImmediate(n);
                Main.Log("Cleared name");
                return true;
            }
            if (!n)
                n = flag == 1 ? (Nickname)g.AddComponent<SlimeNickname>() : g.AddComponent<GordoNickname>();
            n.Name = args.Join(null, " ");
            if (n is GordoNickname)
                (n as GordoNickname).UpdateModel();
            Main.Log("Name has been set to " + n.Name);
            return true;
        }
    }

    class Nickname : SRBehaviour
    {
        public string Name = "";
    }

    class SlimeNickname : Nickname, ExtendedData.Participant
    {
        public void ReadData(CompoundDataPiece piece) => Name = piece.GetValue<string>("nickname");

        public void WriteData(CompoundDataPiece piece) => piece.SetValue("nickname", Name);
    }

    class GordoNickname : Nickname
    {
        public string id;
        void Awake()
        {
            var gordo = GetComponent<GordoEat>();
            if (gordo.id != null)
                id = gordo.id;
            else if (gordo.snareModel != null)
                id = gordo.snareModel.siteId;
        }
        public void UpdateModel()
        {
            if (id == null)
                Console.LogWarning(this + " cannot update model due to null id");
            else
                Main.names[id] = Name;
        }
        public void OnDestroy()
        {
            if (Main.names.ContainsKey(id))
                Main.names.Remove(id);
        }
    }

    [HarmonyPatch(typeof(TargetingUI), "GetIdentifiableTarget")]
    class Patch_GetTargetName
    {
        public static void Postfix(TargetingUI __instance, GameObject gameObject, bool __result)
        {
            if (!__result)
                return;
            var n = gameObject.GetComponent<Nickname>();
            if (n && !string.IsNullOrEmpty(n.Name))
                __instance.nameText.text = n.Name;
        }
    }

    [HarmonyPatch(typeof(TargetingUI), "GetGordoIdentifiableTarget")]
    class Patch_GetTargetName2
    {
        static void Postfix(TargetingUI __instance, GameObject gameObject, bool __result) => Patch_GetTargetName.Postfix(__instance, gameObject, __result);
    }

    [HarmonyPatch(typeof(GordoEat), "SetModel", typeof(GordoModel))]
    class Patch_InitGordoData
    {
        static void Postfix(GordoEat __instance)
        {
            if (Main.names.ContainsKey(__instance.id)) __instance.gameObject.AddComponent<GordoNickname>().Name = Main.names[__instance.id];
        }
    }

    [HarmonyPatch(typeof(GordoEat), "SetModel", typeof(GadgetModel))]
    class Patch_InitGordoData2
    {
        static void Postfix(GordoEat __instance, GadgetModel model)
        {
            if (Main.names.ContainsKey(model.siteId)) __instance.gameObject.AddComponent<GordoNickname>().Name = Main.names[model.siteId];
        }
    }

}