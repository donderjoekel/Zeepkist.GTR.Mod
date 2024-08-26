using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using UnityEngine;
using ILogger = Microsoft.Extensions.Logging.ILogger;

namespace TNRD.Zeepkist.GTR.PlayerLoop;

public partial class PlayerLoopService
{
    private class PlayerLoopBehaviour : MonoBehaviour
    {
        private readonly Dictionary<PlayerLoopSubscription, Action> _updates = new();
        private readonly Dictionary<PlayerLoopSubscription, Action> _fixedUpdates = new();

        private readonly HashSet<PlayerLoopSubscription> _updatesToRemove = new();
        private readonly HashSet<PlayerLoopSubscription> _fixedUpdatesToRemove = new();

        private ILogger _logger;

        public PlayerLoopSubscription SubscribeUpdate(Action action)
        {
            if (action == null)
                throw new ArgumentNullException(nameof(action));

            PlayerLoopSubscription subscription = new();
            _updates.Add(subscription, action);
            return subscription;
        }

        public void UnsubscribeUpdate(PlayerLoopSubscription subscription)
        {
            if (subscription == null)
                throw new ArgumentNullException(nameof(subscription));

            _updatesToRemove.Add(subscription);
        }

        public PlayerLoopSubscription SubscribeFixedUpdate(Action action)
        {
            if (action == null)
                throw new ArgumentNullException(nameof(action));

            PlayerLoopSubscription subscription = new();
            _fixedUpdates.Add(subscription, action);
            return subscription;
        }

        public void UnsubscribeFixedUpdate(PlayerLoopSubscription subscription)
        {
            if (subscription == null)
                throw new ArgumentNullException(nameof(subscription));

            _fixedUpdatesToRemove.Add(subscription);
        }

        private void Update()
        {
            foreach (PlayerLoopSubscription subscription in _updatesToRemove)
            {
                _updates.Remove(subscription);
            }

            _updatesToRemove.Clear();

            foreach (KeyValuePair<PlayerLoopSubscription, Action> kvp in _updates)
            {
                try
                {
                    kvp.Value();
                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                    throw;
                }
            }
        }

        private void FixedUpdate()
        {
            foreach (PlayerLoopSubscription subscription in _fixedUpdatesToRemove)
            {
                _fixedUpdates.Remove(subscription);
            }

            _fixedUpdatesToRemove.Clear();

            foreach (KeyValuePair<PlayerLoopSubscription, Action> kvp in _fixedUpdates)
            {
                try
                {
                    kvp.Value();
                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                    throw;
                }
            }
        }

        public void SetLogger(ILogger logger)
        {
            _logger = logger;
        }
    }
}
