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
        private readonly Dictionary<PlayerLoopSubscription, Action> _lateUpdates = new();

        private readonly HashSet<PlayerLoopSubscription> _updatesToRemove = new();
        private readonly HashSet<PlayerLoopSubscription> _fixedUpdatesToRemove = new();
        private readonly HashSet<PlayerLoopSubscription> _lateUpdatesToRemove = new();

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

        public PlayerLoopSubscription SubscribeLateUpdate(Action action)
        {
            if (action == null)
                throw new ArgumentNullException(nameof(action));

            PlayerLoopSubscription subscription = new();
            _lateUpdates.Add(subscription, action);
            return subscription;
        }

        public void UnsubscribeLateUpdate(PlayerLoopSubscription subscription)
        {
            if (subscription == null)
                throw new ArgumentNullException(nameof(subscription));

            _lateUpdatesToRemove.Add(subscription);
        }

        private void Update()
        {
            Dispatch(_updates, _updatesToRemove, "Update");
        }

        private void FixedUpdate()
        {
            Dispatch(_fixedUpdates, _fixedUpdatesToRemove, "FixedUpdate");
        }

        private void LateUpdate()
        {
            Dispatch(_lateUpdates, _lateUpdatesToRemove, "LateUpdate");
        }

        private void Dispatch(
            Dictionary<PlayerLoopSubscription, Action> subscriptions,
            HashSet<PlayerLoopSubscription> subscriptionsToRemove,
            string phase)
        {
            RemovePendingSubscriptions(subscriptions, subscriptionsToRemove);

            foreach (KeyValuePair<PlayerLoopSubscription, Action> subscription in subscriptions)
            {
                try
                {
                    subscription.Value();
                }
                catch (Exception exception)
                {
                    _logger.LogError(
                        exception,
                        "Player loop {Phase} subscription failed; unsubscribing {Subscription}",
                        phase,
                        subscription.Key);
                    subscriptionsToRemove.Add(subscription.Key);
                }
            }

            RemovePendingSubscriptions(subscriptions, subscriptionsToRemove);
        }

        private static void RemovePendingSubscriptions(
            Dictionary<PlayerLoopSubscription, Action> subscriptions,
            HashSet<PlayerLoopSubscription> subscriptionsToRemove)
        {
            foreach (PlayerLoopSubscription subscription in subscriptionsToRemove)
                subscriptions.Remove(subscription);

            subscriptionsToRemove.Clear();
        }

        public void SetLogger(ILogger logger)
        {
            _logger = logger;
        }
    }
}
