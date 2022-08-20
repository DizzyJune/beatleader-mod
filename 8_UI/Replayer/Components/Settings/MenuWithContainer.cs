﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Zenject;

namespace BeatLeader.Components.Settings
{
    internal class MenuWithContainer : Menu
    {
        protected DiContainer Container { get; private set; }

        public static T InstantiateInContainer<T>(DiContainer container) where T : MenuWithContainer
        {
            var menu = (MenuWithContainer)Activator.CreateInstance(typeof(T));
            container.Inject(menu);
            menu.Container = container;
            menu.OnInstantiate();
            menu.Handle();
            return (T)menu;
        }

        protected override void OnInstantiate() { }
    }
}
