﻿// Copyright (c) Dolittle. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// Sample code for the tutorial at https://dolittle.io/tutorials/embeddings/

using System;
using System.Linq;
using System.Threading.Tasks;
using Dolittle.SDK;
using Dolittle.SDK.Tenancy;

namespace Kitchen
{
    class Program
    {
        public static async Task Main()
        {
            var client = DolittleClient
                .ForMicroservice("f39b1f61-d360-4675-b859-53c05c87c0e6")
                .WithEventTypes(eventTypes =>
                {
                    eventTypes.Register<EmployeeHired>();
                    eventTypes.Register<EmployeeTransferred>();
                    eventTypes.Register<EmployeeRetired>();
                })
                .WithEmbeddings(builder =>
                    builder.RegisterEmbedding<Employee>())
                .Build();
            _ = client.Start();

            // wait for the registration to complete
            await Task.Delay(TimeSpan.FromSeconds(1)).ConfigureAwait(false);

            // mock of the state from the external HR system
            var updatedEmployee = new Employee
            {
                Name = "Mr. Taco",
                Workplace = "Street Food Taco Truck"
            };

            await client.Embeddings
                .ForTenant(TenantId.Development)
                .Update(updatedEmployee.Name, updatedEmployee);
            Console.WriteLine($"Updated {updatedEmployee.Name}.");

            var mrTaco = await client.Embeddings
                .ForTenant(TenantId.Development)
                .Get<Employee>("Mr. Taco")
                .ConfigureAwait(false);
            Console.WriteLine($"Mr. Taco is now working at {mrTaco.State.Workplace}");

            var allEmployeeNames = await client.Embeddings
                .ForTenant(TenantId.Development)
                .GetKeys<Employee>()
                .ConfigureAwait(false);
            Console.WriteLine($"All current employees are {string.Join(",", allEmployeeNames)}");

            await client.Embeddings
                .ForTenant(TenantId.Development)
                .Delete<Employee>(updatedEmployee.Name);
            Console.WriteLine($"Deleted {updatedEmployee.Name}.");

            // wait for the processing to finish before severing the connection
            await Task.Delay(TimeSpan.FromSeconds(1)).ConfigureAwait(false);
        }
    }
}