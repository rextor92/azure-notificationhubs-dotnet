// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for
// license information.

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.NotificationHubs;
using Microsoft.Azure.NotificationHubs.Messaging;
using Microsoft.Extensions.Configuration;

namespace SendPushSample
{
    class Program
    {
        private const string FcmV1SampleNotificationContent = "{\"message\":{\"data\":{\"message\":\"Notification Hub test notification from SDK sample\"}}}";
        private const string FcmV1SampleSilentNotificationContent = "{ \"message\":{\"data\":{ \"Nick\": \"Mario\", \"body\": \"great match!\", \"Room\": \"PortugalVSDenmark\" } }}";
        private const string AppleSampleNotificationContent = "{\"aps\":{\"alert\":\"Notification Hub test notification from SDK sample\"}}";
        private const string AppleSampleSilentNotificationContent = "{\"aps\":{\"content-available\":1}, \"foo\": 2 }";
        private const string WnsSampleNotification = "<?xml version=\"1.0\" encoding=\"utf-8\"?><toast><visual><binding template=\"ToastText01\"><text id=\"1\">Notification Hub test notification from SDK sample</text></binding></visual></toast>";

        static async Task Main(string[] args)
        {
            // Getting connection key from the new resource
            var config = LoadConfiguration(args);
            var nhClient = NotificationHubClient.CreateClientFromConnectionString(config.PrimaryConnectionString, config.HubName);

            // Register some fake devices
            var fcmV1DeviceId = Guid.NewGuid().ToString();
            var fcmV1Installation = new Installation
            {
                InstallationId = "fake-fcmv1-install-id",
                Platform = NotificationPlatform.FcmV1,
                PushChannel = fcmV1DeviceId,
                PushChannelExpired = false,
                Tags = new[] { "fcmv1" }
            };
            await nhClient.CreateOrUpdateInstallationAsync(fcmV1Installation);

            var appleDeviceId = "00fc13adff785122b4ad28809a3420982341241421348097878e577c991de8f0";
            var apnsInstallation = new Installation
            {
                InstallationId = "fake-apns-install-id",
                Platform = NotificationPlatform.Apns,
                PushChannel = appleDeviceId,
                PushChannelExpired = false,
                Tags = new[] { "apns" }
            };
            await nhClient.CreateOrUpdateInstallationAsync(apnsInstallation);

            switch ((SampleConfiguration.Operation)Enum.Parse(typeof(SampleConfiguration.Operation), config.SendType))
            {
                case SampleConfiguration.Operation.Broadcast:
                    // Notification groups should be created on client side
                    var outcomeFcm = await nhClient.SendFcmV1NativeNotificationAsync(FcmV1SampleNotificationContent);
                    await GetPushDetailsAndPrintOutcome("FCMV1", nhClient, outcomeFcm);

                    var outcomeSilentFcm = await nhClient.SendFcmV1NativeNotificationAsync(FcmV1SampleSilentNotificationContent);
                    await GetPushDetailsAndPrintOutcome("FCMV1 Silent", nhClient, outcomeSilentFcm);

                    // Send groupable notifications to iOS
                    var notification = new AppleNotification(AppleSampleNotificationContent);
                    if (!string.IsNullOrEmpty(config.AppleGroupId))
                    {
                        notification.Headers.Add("apns-collapse-id", config.AppleGroupId);
                    }

                    var outcomeApns = await nhClient.SendNotificationAsync(notification);
                    await GetPushDetailsAndPrintOutcome("APNS", nhClient, outcomeApns);

                    var outcomeSilentApns = await nhClient.SendAppleNativeNotificationAsync(AppleSampleSilentNotificationContent);
                    await GetPushDetailsAndPrintOutcome("APNS Silent", nhClient, outcomeSilentApns);

                    var outcomeWns = await nhClient.SendWindowsNativeNotificationAsync(WnsSampleNotification);
                    await GetPushDetailsAndPrintOutcome("WNS", nhClient, outcomeWns);

                    break;
                case SampleConfiguration.Operation.SendByTag:
                    // Send notifications by tag
                    var outcomeFcmByTag = await nhClient.SendFcmV1NativeNotificationAsync(FcmV1SampleNotificationContent, config.Tag ?? "fcmv1");
                    await GetPushDetailsAndPrintOutcome("FCMV1 Tags", nhClient, outcomeFcmByTag);

                    var outcomeApnsByTag = await nhClient.SendAppleNativeNotificationAsync(AppleSampleNotificationContent, config.Tag ?? "apns");
                    await GetPushDetailsAndPrintOutcome("APNSV1 Tags", nhClient, outcomeApnsByTag);

                    break;
                case SampleConfiguration.Operation.SendByDevice:
                    // Send notifications by deviceId
                    var outcomeFcmByDeviceId = await nhClient.SendDirectNotificationAsync(CreateFcmV1Notification(), config.FcmV1DeviceId ?? fcmV1DeviceId);
                    await GetPushDetailsAndPrintOutcome("FCM Direct", nhClient, outcomeFcmByDeviceId);

                    var outcomeApnsByDeviceId = await nhClient.SendDirectNotificationAsync(CreateApnsNotification(), config.AppleDeviceId ?? appleDeviceId);
                    await GetPushDetailsAndPrintOutcome("APNS Direct", nhClient, outcomeApnsByDeviceId);

                    break;
                default:
                    Console.WriteLine("Invalid Sendtype");
                    break;
            }
        }

        private static Notification CreateFcmV1Notification()
        {
            return new FcmNotification(FcmV1SampleNotificationContent);
        }

        private static Notification CreateApnsNotification()
        {
            return new AppleNotification(AppleSampleNotificationContent);
        }

        private static async Task<NotificationDetails> WaitForThePushStatusAsync(string pnsType, NotificationHubClient nhClient, NotificationOutcome notificationOutcome)
        {
            var notificationId = notificationOutcome.NotificationId;
            var state = NotificationOutcomeState.Enqueued;
            var count = 0;
            NotificationDetails outcomeDetails = null;
            while ((state == NotificationOutcomeState.Enqueued || state == NotificationOutcomeState.Processing) && ++count < 10)
            {
                try
                {
                    Console.WriteLine($"{pnsType} status: {state}");
                    outcomeDetails = await nhClient.GetNotificationOutcomeDetailsAsync(notificationId);
                    state = outcomeDetails.State;
                }
                catch (MessagingEntityNotFoundException)
                {
                    // It's possible for the notification to not yet be enqueued, so we may have to swallow an exception
                    // until it's ready to give us a new state.
                }
                Thread.Sleep(1000);
            }
            return outcomeDetails;
        }

        private static void PrintPushOutcome(string pnsType, NotificationDetails details, NotificationOutcomeCollection collection)
        {
            if (collection != null)
            {
                Console.WriteLine($"{pnsType} outcome: " + string.Join(",", collection.Select(kv => $"{kv.Key}:{kv.Value}")));
            }
            else
            {
                Console.WriteLine($"{pnsType} no outcomes.");
            }
            Console.WriteLine($"{pnsType} error details URL: {details.PnsErrorDetailsUri}");
        }

        private static void PrintPushNoOutcome(string pnsType)
        {
            Console.WriteLine($"{pnsType} has no outcome due to it is only available for Standard SKU pricing tier.");
        }

        private static async Task GetPushDetailsAndPrintOutcome(
            string pnsType,
            NotificationHubClient nhClient,
            NotificationOutcome notificationOutcome)
        {
            // The Notification ID is only available for Standard SKUs. For Basic and Free SKUs the API to get notification outcome details can not be called.
            if (string.IsNullOrEmpty(notificationOutcome.NotificationId))
            {
                PrintPushNoOutcome(pnsType);
                return;
            }

            var details = await WaitForThePushStatusAsync(pnsType, nhClient, notificationOutcome);
            NotificationOutcomeCollection collection = null;
            switch (pnsType)
            {
                case "FCMV1":
                case "FCMV1 Silent":
                case "FCMV1 Tags":
                case "FCMV1 Direct":
                    collection = details.FcmOutcomeCounts;
                    break;

                case "APNS":
                case "APNS Silent":
                case "APNS Tags":
                case "APNS Direct":
                    collection = details.ApnsOutcomeCounts;
                    break;

                case "WNS":
                    collection = details.WnsOutcomeCounts;
                    break;
                default:
                    Console.WriteLine("Invalid Sendtype");
                    break;
            }

            PrintPushOutcome(pnsType, details, collection);
        }

        private static SampleConfiguration LoadConfiguration(string[] args)
        {
            var configurationBuilder = new ConfigurationBuilder()
                .AddJsonFile("config.json", true)
                .AddCommandLine(args)
                .Build();

            var sampleConfig = new SampleConfiguration();
            configurationBuilder.Bind(sampleConfig);
            return sampleConfig;
        }
    }
}
