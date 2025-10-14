$tunnelName = "xxxxxxxxxxxxxx" # replace with your tunnel name

#only first time when creating the tunnel
#devtunnel create $tunnelName --allow-anonymous
#devtunnel port create -p 7284 --protocol https

devtunnel update $tunnelName --expiration 30d
devtunnel host $tunnelName