# LocalAzureAgent
A simple build agent, which can run on a local .Net Core host and build source code using a Azure DevOps compliant yaml file.

The aim of the project is to assist the local validation of build yaml scripts prior to committing to Azure DevOps repos. 

The built utility is suitable for one hit compilation as a command line utility, or alternatively, can be installed and run as service, and continuously executing the pipeline.

