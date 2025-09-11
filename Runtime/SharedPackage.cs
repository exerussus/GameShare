using System.Collections.Generic;
using UnityEngine;

namespace Exerussus.GameSharing.Runtime
{
    public class SharedPackage : MonoBehaviour, IGameSharable
    {
        public List<MonoBehaviour> sharedMonoObjects = new();

        public static void GetSharedObjectsFromScene(GameShare gameShare)
        {
            var sharedPackages = FindObjectsByType<SharedPackage>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var sharedPackage in sharedPackages) sharedPackage.ShareWith(gameShare);
        }
        
        public void ShareWith(GameShare gameShare)
        {
            foreach (var mono in sharedMonoObjects)
            {
                gameShare.AddSharedObject(mono);
            }
        }
    }
}