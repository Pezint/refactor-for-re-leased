using System;
using System.Linq;
using RefactorThis.Persistence;

namespace RefactorThis.Domain
{
    public class InvoiceService
    {
        private readonly InvoiceRepository _invoiceRepository;

        public InvoiceService(InvoiceRepository invoiceRepository)
        {
            _invoiceRepository = invoiceRepository;
        }

        public string ProcessPayment(Payment payment)
        {
            var invoice = GetAndValidateInvoice(payment.Reference);

            // Check if the invoice is fully paid
            if (IsInvoiceFullyPaid(invoice))
            {
                return "invoice was already fully paid";
            }

            // Check if the invoice requires any payment
            if (invoice.Amount == 0)
            {
                return "no payment needed"; 
            }

            // Validate if the payment amount is correct
            if (!IsPaymentAmountValid(invoice, payment.Amount))
            {
                return GetInvalidPaymentAmountMessage(invoice); // Return the relevant 'invalid payment amount' message for the invoice
            }

            ApplyPaymentToInvoice(invoice, payment); // Apply the payment to the invoice
            invoice.Save(); // Save the updated invoice to the repository

            return GetPaymentConfirmationMessage(invoice, payment.Amount); // Return the confirmation message
        }

        private Invoice GetAndValidateInvoice(string reference)
        {
            var invoice = _invoiceRepository.GetInvoice(reference); // Retrieve the invoice by reference

            if (invoice == null)
            {
                throw new InvalidOperationException("There is no invoice matching this payment"); // Throw an error if no invoice is found
            }

            if (invoice.Amount == 0 && invoice.Payments != null && invoice.Payments.Any()) // Check if the invoice is in an invalid state
            {
                throw new InvalidOperationException("The invoice is in an invalid state, it has an amount of 0 and it has payments.");
            }

            return invoice; // Return the valid invoice
        }

        private bool IsInvoiceFullyPaid(Invoice invoice)
        {
            return invoice.Payments != null && // Payments on the invoice is not null
                   invoice.Payments.Any() && // At least one payment exists on the invoice
                   invoice.Payments.Sum(x => x.Amount) != 0 && // The sum of the payments on the invoice is not 0
                   invoice.Amount == invoice.Payments.Sum(x => x.Amount); // Check if the total payments equal the invoice amount
        }

        private bool IsPaymentAmountValid(Invoice invoice, decimal paymentAmount)
        {
            decimal remainingAmount = invoice.Amount - invoice.AmountPaid; // Calculate remaining amount to be paid

            // If the invoice has existing payments
            if (HasExistingPayments(invoice))
            {
                return paymentAmount <= remainingAmount; // Check that the payment is not more than the remaining amount
            }

            return paymentAmount <= invoice.Amount; // Check that the first payment to the invoice is not more than the full invoice amount
        }

        private string GetInvalidPaymentAmountMessage(Invoice invoice)
        {
            // If the invoice has existing payments
            if (HasExistingPayments(invoice))
            {
                return "the payment is greater than the partial amount remaining";
            }

            return "the payment is greater than the invoice amount"; // Error message when the invalid payment amount is the first payment to the invoice
        }

        private bool HasExistingPayments(Invoice invoice)
        {
            // Check if there are any existing payments on the invoice
            return invoice.Payments != null &&
                   invoice.Payments.Any() &&
                   invoice.Payments.Sum(x => x.Amount) != 0;
        }

        private void ApplyPaymentToInvoice(Invoice invoice, Payment payment)
        {
            if (invoice.Payments == null)
            {
                invoice.Payments = new System.Collections.Generic.List<Payment>(); // Initialise a payment list if null
            }

            invoice.AmountPaid += payment.Amount; // Add payment amount to the total amount paid

            // Apply 14% tax if the invoice is commercial
            if (invoice.Type == InvoiceType.Commercial)
            {
                invoice.TaxAmount += payment.Amount * 0.14m;

            }

            // Add the payment to the list of payments for the invoice
            invoice.Payments.Add(payment);
        }

        private string GetPaymentConfirmationMessage(Invoice invoice, decimal paymentAmount)
        {
            // Calculate the remaining amount on the invoice
            decimal remainingAmount = invoice.Amount - invoice.AmountPaid;

            // Message options for if the invoice is fully paid
            if (remainingAmount == 0)
            {
                // Check if the invoice is fully paid after this partial payment
                if (HasExistingPayments(invoice) && invoice.AmountPaid != paymentAmount)
                {
                    return "final partial payment received, invoice is now fully paid";
                }

                return "invoice is now fully paid";
            }

            // Message for if the invoice is still only partially paid
            if (HasExistingPayments(invoice) && invoice.AmountPaid != paymentAmount)
            {
                return "another partial payment received, still not fully paid";
            }

            return "invoice is now partially paid";
        }
    }
}
