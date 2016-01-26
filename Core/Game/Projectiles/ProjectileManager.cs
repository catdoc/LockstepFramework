﻿using UnityEngine;
using System.Collections;
using System;
using Lockstep.Data;
using System.Collections.Generic;
namespace Lockstep {
    public static class ProjectileManager {
		public const int MaxProjectiles = 1 << 13;
        private static string[] AllProjCodes;
        private static readonly Dictionary<string,IProjectileData> CodeDataMap = new Dictionary<string, IProjectileData>();


		public static void Setup ()
        {
           IProjectileDataProvider prov;
            if (LSDatabaseManager.TryGetDatabase<IProjectileDataProvider> (out prov)) {
                IProjectileData[] projectileData = prov.ProjectileData;
                for (int i = 0; i < projectileData.Length; i++)
                {
                    IProjectileData item = projectileData[i];
                    CodeDataMap.Add(item.Name, item);
                    ProjectilePool.Add(item.Name, new FastStack<LSProjectile> ());
                }
            }
        }
        public static void Initialize ()
        {
        }
        public static void Simulate ()
        {
            for (int i = ProjectileBucket.PeakCount - 1; i >= 0; i--)
			{
                if (ProjectileBucket.arrayAllocation[i])
				{
					ProjectileBucket[i].Simulate ();
				}
			}
        }
        public static void Visualize ()
	    {
            for (int i = ProjectileBucket.PeakCount - 1; i >= 0; i--)
			{
                if (ProjectileBucket.arrayAllocation[i])
				{
					ProjectileBucket[i].Visualize ();
				}
			}
        }

		public static void Deactivate ()
		{
            for (int i = ProjectileBucket.PeakCount - 1; i >= 0; i--)
			{
                if (ProjectileBucket.arrayAllocation[i])
				{
					EndProjectile (ProjectileBucket[i]);
				}
			}
		}

        public static int GetStateHash () {
            int hash = 23;
            for (int i = ProjectileBucket.PeakCount - 1; i>= 0; i--) {
                if (ProjectileBucket.arrayAllocation[i]) {
                    LSProjectile proj = ProjectileManager.ProjectileBucket[i];
                    hash ^= proj.GetStateHash ();
                }
            }
            return hash;
        }

        private static LSProjectile NewProjectile (string projCode)
		{
            IProjectileData projData = CodeDataMap[projCode];
            curProj = ((GameObject)GameObject.Instantiate<GameObject> (projData.GetProjectile().gameObject)).GetComponent<LSProjectile> ();
			curProj.Setup (projData);
			return curProj;
		}
        public static LSProjectile Create (string projCode, LSAgent source, Vector3d offset, AllegianceType targetAllegiance, Func<LSAgent,bool> agentConditional,Action<LSAgent> hitEffect) {
            Vector3d pos = offset;
            pos.SetVector2d(pos.ToVector2d().Rotated(source.Body._rotation.x,source.Body._rotation.y));
            pos.Add(ref source.Body._position);
            return Create (projCode,pos,agentConditional,(bite) => ((source.Controller.GetAllegiance(bite) & targetAllegiance) != 0),hitEffect);
        }
        public static LSProjectile Create (string projCode, Vector3d position, Func<LSAgent,bool> agentConditional, Func<byte,bool> bucketConditional, Action<LSAgent> hitEffect)
		{
            curProj = RawCreate (projCode);

            int id = ProjectileBucket.Add(curProj);
			curProj.Prepare (id, position,agentConditional,bucketConditional, hitEffect, true);
			return curProj;
		}
        private static LSProjectile RawCreate (string projCode) {
            FastStack<LSProjectile> pool = ProjectilePool[projCode];
            if (pool.Count > 0)
            {
                curProj = pool.Pop ();
            }
            else {
                curProj = NewProjectile (projCode);
            } 
            return curProj;
        }
		public static void Fire (LSProjectile projectile)
		{
			projectile.LateInit ();
		}

        private static FastBucket<LSProjectile> NDProjectileBucket = new FastBucket<LSProjectile>();
        public static void NDCreateAndFire (string projCode, Vector3d position, Vector3d direction, bool gravity = false) {
            curProj = RawCreate (projCode);
            int id = NDProjectileBucket.Add (curProj);
            curProj.Prepare(id,position,(a)=>false,(a)=>false,(a)=>{}, false);
        }

		public static void EndProjectile (LSProjectile projectile)
		{
            if (projectile.Deterministic) {
    			int id = projectile.ID;
                if(!ProjectileBucket.SafeRemoveAt(id,projectile)) {
                    Debug.Log("BOO! This is a terrible bug.");
                }
            }
            else {
                if (!NDProjectileBucket.SafeRemoveAt(projectile.ID,projectile)) {
                    Debug.Log("BOO! This is a terrible bug.");
                }
            }
			CacheProjectile (projectile);
			projectile.Deactivate ();
		}

		#region ID and allocation management
        private static readonly Dictionary<string, FastStack<LSProjectile>> ProjectilePool = new Dictionary<string, FastStack<LSProjectile>>();
        private static FastBucket<LSProjectile> ProjectileBucket = new FastBucket<LSProjectile>();

		private static void CacheProjectile (LSProjectile projectile)
		{
			ProjectilePool[projectile.MyProjCode].Add (projectile);
			/*if (projectile.ID == PeakCount - 1)
			{
				PeakCount--;
				for (int i = projectile.ID - 1; i >= 0; i--)
				{
					if (ProjectileActive[i] == false)
					{
						PeakCount--;
					}
					else {
						break;
					}
				}
			}*/
		}
		#endregion

		#region Helpers
		static LSAgent curAgent;
		static LSProjectile curProj;
		#endregion
    }

}