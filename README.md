# ModbusRegService
this  project is simple windows service - collecting modbus regs and store to sql base...

this project used NModbus Lib.. licensed at MIT.. 
NModbus is a C# 3.0 implementation of the Modbus protocol. 
More information at the NModbus project web site http://nmodbus.com/


карочи я не хрена не понимаю как пользоваться важим гитхабом.. 
да я использовал библиотеку Nmodbus но она по лицензии мит.. 
я должен её умпомянуть, на сколько я понимаю это чертовы юридические тонкости.. 
если кто может мне всё эту фигню пояснить -
то свяжитесь со мной.
neco@ngs.ru


теперь по порядку:
это програмная штуковина предназначена для сбора данных с регистров модбас и сохранения в базу. 
настройки базы и регистров есть в файле сеттингс.
основное и, главное отличие, от аналогов - она стартует как windows-service. на русском служба. не требует логина и входа в комп. 
но требует админских прав для установки... 
от админа команда: ModbusRegService.exe  /install

все исходники прилагаются, я надеюсь, что ничьи права я не нарушил. 



