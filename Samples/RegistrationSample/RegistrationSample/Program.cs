// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for
// license information.

using System;
using System.Threading.Tasks;
using Microsoft.Azure.NotificationHubs;
using Microsoft.Azure.NotificationHubs.Messaging;
using Microsoft.Extensions.Configuration;

namespace RegistrationSample
{
    class Program
    {
        static async Task Main(string[] args)
        {
            // Getting connection key from the new resource
            var config = LoadConfiguration(args);
            var nhClient = NotificationHubClient.CreateClientFromConnectionString(config.PrimaryConnectionString, config.HubName);
            await CreateAndDeleteInstallationAsync(nhClient);
            await CreateAndDeleteRegistrationAsync(nhClient);
        }

        private static async Task CreateAndDeleteRegistrationAsync(NotificationHubClient nhClient)
        {
            var registrationId = await nhClient.CreateRegistrationIdAsync();
            var registrationDescr = await nhClient.CreateFcmV1NativeRegistrationAsync(registrationId);
            Console.WriteLine($"Created FCM v1 registration {registrationDescr.FcmV1RegistrationId}");

            var allRegistrations = await nhClient.GetAllRegistrationsAsync(1000);
            foreach (var regFromServer in allRegistrations)
            {
                if (regFromServer.RegistrationId == registrationDescr.RegistrationId)
                {
                    Console.WriteLine($"Found FCM v1 registration {registrationDescr.FcmV1RegistrationId}");
                    break;
                }
            }

            //registrationDescr = await nhClient.GetRegistrationAsync<FcmV1RegistrationDescription>(registrationId);
            //Console.WriteLine($"Retrieved FCM v1 registration {registrationDescr.FcmV1RegistrationId}");

            await nhClient.DeleteRegistrationAsync(registrationDescr);
            Console.WriteLine($"Deleted FCM v1 registration {registrationDescr.FcmV1RegistrationId}");
        }

        private static async Task CreateAndDeleteInstallationAsync(NotificationHubClient nhClient)
        {
            // Register some fake devices
            var fcmV1DeviceId = Guid.NewGuid().ToString();
            var fcmV1Installation = new Installation
            {
                InstallationId = fcmV1DeviceId,
                Platform = NotificationPlatform.FcmV1,
                PushChannel = fcmV1DeviceId,
                PushChannelExpired = false,
                Tags = new[] { "fcmv1" }
            };
            await nhClient.CreateOrUpdateInstallationAsync(fcmV1Installation);

            while (true)
            {
                try
                {
                    var installationFromServer = await nhClient.GetInstallationAsync(fcmV1Installation.InstallationId);
                    break;
                }
                catch (MessagingEntityNotFoundException)
                {
                    // Wait for installation to be created
                    await Task.Delay(1000);
                }
            }
            Console.WriteLine($"Created FCM v1 installation {fcmV1Installation.InstallationId}");
            await nhClient.DeleteInstallationAsync(fcmV1Installation.InstallationId);
            while (true)
            {
                try
                {
                    var installationFromServer = await nhClient.GetInstallationAsync(fcmV1Installation.InstallationId);
                    await Task.Delay(1000);
                }
                catch (MessagingEntityNotFoundException)
                {
                    Console.WriteLine($"Deleted FCM v1 installation {fcmV1Installation.InstallationId}");
                    break;
                }
            }
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
