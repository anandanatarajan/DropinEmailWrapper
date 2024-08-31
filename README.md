# EmailLibrary

A simple wraper library implements outbox pattern for sending email

it has both the inbuilt sqlite as well as supports consumers datacontext

sends mail via hangfire background job so hangfire should be injected to this library
it uses mailkit

the to and cc can be passed as comma seperated values,
the settings can be read and bind to the mail settings class from the external conf or json file

