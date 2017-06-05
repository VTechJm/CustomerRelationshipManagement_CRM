﻿using CRM.WebApp.Models;
using EntityLibrary;
//using LinqToExcel;
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Data.Entity;
using System.Data.Entity.Core;
using System.Data.Entity.Infrastructure;
using System.Data.Entity.Migrations;
using System.Data.SqlClient;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web;
using System.Web.Http;
using System.Web.Http.Description;
using System.Web.Http.ModelBinding;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;

namespace CRM.WebApp.Infrastructure
{
    public class ApplicationManager : IDisposable
    {

        // public ApplicationManager(){}
        DataBaseCRMEntityes db = new DataBaseCRMEntityes();
        ModelFactory factory = new ModelFactory();
        #region Contacts
        public async Task<List<ContactResponseModel>> GetAllContacts()
        {

            try
            {
                db.Configuration.LazyLoadingEnabled = false;
                List<Contact> dbContactList = await db.Contacts.ToListAsync();
                List<ContactResponseModel> responseContactList = new List<ContactResponseModel>();
                return dbContactList.Select(x => factory.CreateContactResponseModel(x)).ToList();
            }
            catch (EntitySqlException dbEx)
            {

                throw new EntitySqlException(dbEx.Message);
            }

        }
        public async Task<List<Contact>> GetContactPage(int start, int numberRows, bool flag)
        {
            var query = await db.Contacts.OrderBy(x => x.DateInserted).Skip(start).Take(numberRows).ToListAsync();

            for (int i = 0; i < query.Count; i++)
            {
                query[i].EmailLists = new List<EmailList>();
            }
            return query;
        }

        public async Task<ContactResponseModel> GetContactByGuid(Guid id)
        {
            var contact = await db.Contacts.FirstOrDefaultAsync(t => t.GuID == id);

            return factory.CreateContactResponseModel(contact);
        }

        public async Task<int> GetContactsPageCounter()
        {
            return await db.Contacts.CountAsync() > 10 ? await db.Contacts.CountAsync() / 10 : 1;
        }

        public async Task<List<ContactResponseModel>> GetContactsByGuIdList(List<Guid> GuIdList)
        {
            List<ContactResponseModel> ContactsList = new List<ContactResponseModel>();
            foreach (var guid in GuIdList)
            {
                ContactsList.Add(await GetContactByGuid(guid));
            }

            return ContactsList;
        }
        public async Task<bool> UpdateContact(Guid guid, ContactRequestModel contact)
        {
            using (DbContextTransaction transaction = db.Database.BeginTransaction())
            {
                Contact dbContactToUpdate;
                try
                {
                    dbContactToUpdate = await db.Contacts.FirstOrDefaultAsync(c => c.GuID == guid);
                }
                catch (Exception)
                {

                    throw;
                }
                if (dbContactToUpdate == null) return false;
                dbContactToUpdate.FullName = contact.FullName;
                dbContactToUpdate.Country = contact.Country;
                dbContactToUpdate.Position = contact.Position;
                dbContactToUpdate.CompanyName = contact.CompanyName;
                dbContactToUpdate.Email = contact.Email;

                db.Entry(dbContactToUpdate).State = EntityState.Modified;
                try
                {
                    await db.SaveChangesAsync();
                    transaction.Commit();
                }
                catch (DbUpdateConcurrencyException)
                {

                    if (!await ContactExistsAsync(guid))
                    {
                        return false;
                    }
                    else
                    {
                        transaction.Rollback();
                        throw;
                    }

                }
                return true;
            }
        }


        public async Task<ContactResponseModel> AddContact(ContactRequestModel contact)
        {
            using (DbContextTransaction transaction = db.Database.BeginTransaction())
            {
                try
                {
                    var contacts = factory.CreateContact(contact);
                    db.Contacts.Add(contacts);
                    await db.SaveChangesAsync();
                    transaction.Commit();
                    var response = factory.CreateContactResponseModel(contacts);
                    return response;
                }
                catch
                {
                    transaction.Rollback();
                    throw;
                }
            }
        }
        public async Task<ContactResponseModel> RemoveContact(Guid guid)
        {
            try
            {
                var contact = await db.Contacts.FirstOrDefaultAsync(c => c.GuID == guid);
                var resModel = factory.CreateContactResponseModel(contact);
                db.Contacts.Remove(contact);
                await db.SaveChangesAsync();
                return resModel;

            }
            catch
            {
                throw;
            }
        }
        public async Task<bool> RemoveContactByGuidList(List<Guid> guidlist)
        {

            foreach (var item in guidlist)
            {
                await RemoveContact(item);
            }
            return true;

        }
        public async Task<bool> ContactExistsAsync(Guid id)
        {
            try
            {
                return await db.Contacts.CountAsync(e => e.GuID == id) > 0;
            }
            catch
            {
                throw;
            }
        }
        #endregion


        #region EmailLists
        public async Task<List<EmailListResponseModel>> GetAllEmailLis()
        {
            try
            {
                List<EmailList> entityContactList = await db.EmailLists.ToListAsync();
                List<EmailListResponseModel> ModelContactList = new List<EmailListResponseModel>();
                return entityContactList.Select(f => factory.CreateEmailResponseModel(f)).ToList();
            }
            catch
            {
                throw;
            }
        }

        public async Task<EmailList> GetEmailListById(int id)
        {
            return await db.EmailLists.FirstOrDefaultAsync(t => t.EmailListID == id); //factory.CreateEmailResponseModel(email);
        }

        public async Task<EmailListResponseModel> AddEmailList(EmailList еmailListForAddOrUpdate, EmailListRequestModel requestEmailListModel)
        {
            using (DbContextTransaction transaction = db.Database.BeginTransaction())
            {
                еmailListForAddOrUpdate.EmailListName = requestEmailListModel.EmailListName;

                if (requestEmailListModel.Contacts != null)
                {
                    еmailListForAddOrUpdate.Contacts.Clear();
                    foreach (Guid guid in requestEmailListModel.Contacts)
                    {
                        var contacts = await db.Contacts.FirstOrDefaultAsync(x => x.GuID == guid);
                        if (contacts != null) еmailListForAddOrUpdate.Contacts.Add(contacts);
                    }
                }
                try
                {
                    db.EmailLists.AddOrUpdate(еmailListForAddOrUpdate);
                    await db.SaveChangesAsync();
                    transaction.Commit();
                }
                catch (Exception)
                {
                    transaction.Rollback();
                    if ((await EmailListExists(еmailListForAddOrUpdate.EmailListID)))
                        return null;
                    else
                        throw;
                }

                return factory.CreateEmailResponseModel(еmailListForAddOrUpdate);
            }
        }
        public async Task<EmailListResponseModel> AddAtEmailList(EmailList еmailListForAddOrUpdate,List<Guid> guidList)
        {
            using (DbContextTransaction transaction = db.Database.BeginTransaction())
            {
                //еmailListForAddOrUpdate.EmailListName = requestEmailListModel.EmailListName;

                if (guidList.Count != 0)
                {
                    foreach (Guid guid in guidList)
                    {
                        var contacts = await db.Contacts.FirstOrDefaultAsync(x => x.GuID == guid);
                        if (contacts != null)
                            еmailListForAddOrUpdate.Contacts.Add(contacts);
                    }
                }
                try
                {

                    await db.SaveChangesAsync();
                    transaction.Commit();
                }
                catch (Exception)
                {
                    transaction.Rollback();
                    if ((await EmailListExists(еmailListForAddOrUpdate.EmailListID)))
                        return null;
                    else
                        throw;
                }

                return factory.CreateEmailResponseModel(еmailListForAddOrUpdate);
            }
        }
        public async Task<EmailListResponseModel> RemoveAtEmailList(EmailList еmailListForAddOrUpdate, List<Guid> guidList)
        {
            using (DbContextTransaction transaction = db.Database.BeginTransaction())
            {
                //еmailListForAddOrUpdate.EmailListName = guidList.EmailListName;

                if (guidList.Count != 0)
                {
                    foreach (Guid guid in guidList)
                    {
                        var contacts = await db.Contacts.FirstOrDefaultAsync(x => x.GuID == guid);
                        if (contacts != null)
                            еmailListForAddOrUpdate.Contacts.Remove(contacts);
                    }
                }
                try
                {

                    await db.SaveChangesAsync();
                    transaction.Commit();
                }
                catch (Exception)
                {
                    transaction.Rollback();
                    if ((await EmailListExists(еmailListForAddOrUpdate.EmailListID)))
                        return null;
                    else
                        throw;
                }

                return factory.CreateEmailResponseModel(еmailListForAddOrUpdate);
            }
        }

        public async Task<EmailListResponseModel> RemoveEmailList(int id)
        {
            EmailList emailList = await db.EmailLists.FindAsync(id);
            db.EmailLists.Remove(emailList);
            await db.SaveChangesAsync();
            return factory.CreateEmailResponseModel(emailList);
        }

        public async Task<bool> EmailListExists(int id)
        {
            return await db.EmailLists.CountAsync(e => e.EmailListID == id) > 0;
        }
        #endregion




        #region Templates
        public async Task<List<TemplateResponseModel>> GetTemplates()
        {
            var templateList = await db.Templates.ToListAsync();
            var response = new List<TemplateResponseModel>();
            return templateList.Select(x => factory.CreateTemplateResponseModel(x)).ToList();
        }
        public async Task<bool> TemplateExistsAsync(int id)
        {
            return await db.Templates.CountAsync(e => e.TemplateId == id) > 0;
        }
        #endregion
        #region uploading
        public async Task<List<ContactResponseModel>> AddContactsFromFile(HttpRequestMessage request)
        {
            string filePath = null;
            using (DbContextTransaction transaction = db.Database.BeginTransaction())
            {
                try
                {
                    

                    string tempPath = System.Web.HttpContext.Current?.Request.MapPath("~//Templates");

                    
                  //  string desctopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);

                    List<ContactResponseModel> response = new List<ContactResponseModel>();
                    List<ContactRequestModel> listOfContactRequests = null;

                    var provider = new MultipartMemoryStreamProvider();
                    await request.Content.ReadAsMultipartAsync(provider);

                    var file = provider.Contents[0];
                    var fileName = file.Headers.ContentDisposition.FileName;
                    var correctedFileName = "new" + fileName.Split('"', '\\').First(s => !string.IsNullOrEmpty(s));
                    filePath = tempPath + '\\' + correctedFileName;
                    string fileExtension = correctedFileName.Split('.').Last();
                    var buffer = await file.ReadAsByteArrayAsync();

                    File.WriteAllBytes(filePath, buffer);

                    if (fileExtension == "xlsx")
                    {
                        listOfContactRequests = ReadExcelFileDOM(filePath);
                    }
                    else
                        if (fileExtension == "csv")
                    {
                        listOfContactRequests = ReadCSVFile(filePath);
                    }
                    else
                    {
                        throw new FileNotFoundException("Wrong extension of file");
                    }

                    //--------------------------
                    List<Contact> resultContacts = listOfContactRequests.Select(s => factory.CreateContact(s)).ToList();

                    foreach (var item in resultContacts)
                    {
                        db.Contacts.Add(item);
                        response.Add(factory.CreateContactResponseModel(item));
                    }
                    await db.SaveChangesAsync();
                    transaction.Commit();
                    return response;


                }
                catch(Exception ex)
                {

                    transaction.Rollback();
                    throw;
                }
                finally
                {
                    File.Delete(filePath);
                }

            }
        }

        private static List<ContactRequestModel> ReadExcelFileDOM(string filename)
        {

            string[] strRowValues = new string[5];
            List<ContactRequestModel> result = new List<ContactRequestModel>();
            ContactRequestModel contact;

            try
            {
                using (SpreadsheetDocument document = SpreadsheetDocument.Open(filename, true))
                {
                    WorkbookPart workbookPart = document.WorkbookPart;
                    IEnumerable<Sheet> Sheets = document.WorkbookPart.Workbook.GetFirstChild<Sheets>().Elements<Sheet>();
                    string relationshipId = Sheets?.First().Id.Value;
                    WorksheetPart worksheetPart = (WorksheetPart)document.WorkbookPart.GetPartById(relationshipId);
                    SheetData sheetData = worksheetPart.Worksheet.GetFirstChild<SheetData>();
                    int i = 1;
                    int j = 0;
                    string value;

                    int[] valueIndexes = new int[5];

                    foreach (Row r in sheetData.Elements<Row>())
                    {
                        //    if (i != 1)
                        //    {
                        foreach (Cell c in r.Elements<Cell>())
                        {
                            if (c == null) continue;
                            value = c.InnerText;
                            if (c.DataType != null)
                            {
                                var stringTable = workbookPart.GetPartsOfType<SharedStringTablePart>().FirstOrDefault();
                                if (stringTable != null)
                                {
                                    value = stringTable.SharedStringTable.
                                        ElementAt(int.Parse(value)).InnerText;
                                }
                            }
                            strRowValues[j] = value;
                            j = j + 1;
                        }

                        if (i == 1)
                        {
                            valueIndexes[0] = Array.IndexOf(strRowValues, "FullName");
                            valueIndexes[1] = Array.IndexOf(strRowValues, "CompanyName");
                            valueIndexes[2] = Array.IndexOf(strRowValues, "Position");
                            valueIndexes[3] = Array.IndexOf(strRowValues, "Country");
                            valueIndexes[4] = Array.IndexOf(strRowValues, "Email");

                            if (valueIndexes.Contains(-1))
                            {
                                throw new FileNotFoundException("Wrong columns in Excel");
                            }
                            j = 0;
                            i = i + 1;
                            continue;
                        }
                        //  }
                        j = 0;
                        i = i + 1;
                        if (strRowValues.Any(p => p == null)) continue;
                        contact = new ContactRequestModel();
                        contact.FullName = strRowValues[valueIndexes[0]];
                        contact.CompanyName = strRowValues[valueIndexes[1]];
                        contact.Position = strRowValues[valueIndexes[2]];
                        contact.Country = strRowValues[valueIndexes[3]];
                        contact.Email = strRowValues[valueIndexes[4]];
                        result.Add(contact);
                    }
                    return result;
                }

            }
            catch (Exception)
            {
                throw;
            }

        }

        static List<ContactRequestModel> ReadCSVFile(string filePath)
        {
            string[] CSVLines = File.ReadAllLines(filePath);
            string[] columnNames = CSVLines[0].Split(';', ',');

            int[] ColumnPositions = new int[columnNames.Length];
            ColumnPositions[0] = Array.IndexOf(columnNames, "FullName");
            ColumnPositions[1] = Array.IndexOf(columnNames, "CompanyName");
            ColumnPositions[2] = Array.IndexOf(columnNames, "Position");
            ColumnPositions[3] = Array.IndexOf(columnNames, "Country");
            ColumnPositions[4] = Array.IndexOf(columnNames, "Email");

            if (ColumnPositions.Contains(-1))
            {
                throw new FileNotFoundException("Wrong column names in CSV");
            }

            List<ContactRequestModel> listOfContactRequests = new List<ContactRequestModel>();
            string[] CellsOfRow;

            for (int i = 1; i < CSVLines.Length; i++)
            {
                CellsOfRow = CSVLines[i].Split(';', ',');

                listOfContactRequests.Add(new ContactRequestModel
                {
                    FullName = CellsOfRow[ColumnPositions[0]],
                    CompanyName = CellsOfRow[ColumnPositions[1]],
                    Position = CellsOfRow[ColumnPositions[2]],
                    Country = CellsOfRow[ColumnPositions[3]],
                    Email = CellsOfRow[ColumnPositions[4]]
                });
            }
            return listOfContactRequests;
        }

        #endregion

        public async Task SaveDb()
        {
            await db.SaveChangesAsync();
        }
        public void Dispose()
        {
            db.Dispose();
        }
    }

}